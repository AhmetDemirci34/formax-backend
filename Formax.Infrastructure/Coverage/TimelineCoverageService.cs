using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Coverage;
using Formax.Application.Interfaces;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Formax.Infrastructure.Coverage
{
    /// <summary>
    /// Timeline Coverage Intelligence — GDP takım Timeline'ının operasyonel görünümünü GERÇEK veriden
    /// (Teams + Matches) hesaplar. Ağır sorgu yok: iki hafif projeksiyon + in-memory agregasyon, 60 sn
    /// cache. Timeline sistemini ETKİLEMEZ (salt-okunur). Quota plan limiti gerçek /status'tan (cached 1h),
    /// alınamazsa config fallback. Tahmin yok — her sayı ölçülür veya koddan sabittir.
    /// </summary>
    public sealed class TimelineCoverageService : ITimelineCoverageService
    {
        private readonly FormaxDbContext _db;
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;
        private readonly ApiFootballMetrics _metrics;
        private readonly TimelineSyncTelemetry _telemetry;
        private readonly ISportsDataProvider _provider;

        private const string CoreCacheKey = "timeline:core:v1";
        private static readonly TimeSpan CoreTtl = TimeSpan.FromSeconds(60);

        public TimelineCoverageService(
            FormaxDbContext db, IConfiguration config, IMemoryCache cache,
            ApiFootballMetrics metrics, TimelineSyncTelemetry telemetry, ISportsDataProvider provider)
        {
            _db = db;
            _config = config;
            _cache = cache;
            _metrics = metrics;
            _telemetry = telemetry;
            _provider = provider;
        }

        // ── Config helpers ──────────────────────────────────────────────────────
        private int HistoricalTarget() => ConfigInt("Timeline:HistoricalTargetMatches", 10, 1, 99);
        private int MaxTeamsPerCycle() => ConfigInt("Timeline:MaxTeamsPerCycle", 40, 1, 1000);
        private int RefreshHours()     => ConfigInt("Timeline:RefreshIntervalHours", 72, 1, 100000);
        private int DailyLimitConfig() => ConfigInt("Timeline:DailyRequestLimit", 7500, 1, 10_000_000);

        private int ConfigInt(string key, int fallback, int min, int max)
            => int.TryParse(_config[key], out var v) && v >= min && v <= max ? v : fallback;

        // ══════════════════════════════ Core snapshot (cached) ══════════════════
        private CoreSnapshot Core()
        {
            if (_cache.TryGetValue(CoreCacheKey, out CoreSnapshot? cached) && cached != null)
                return cached with { FromCache = true };

            var snap = ComputeCore();
            _cache.Set(CoreCacheKey, snap, CoreTtl);
            return snap;
        }

        private CoreSnapshot ComputeCore()
        {
            var now = DateTime.UtcNow;
            var target = HistoricalTarget();

            // Hafif projeksiyonlar (yalnız gerekli kolonlar).
            var teams = _db.Teams
                .Select(t => new { t.Id, t.TimelineSyncedAt })
                .ToList();
            var matches = _db.Matches.Where(m => m.LeagueId > 0)
                .Select(m => new { m.HomeTeamId, m.AwayTeamId, m.MatchDate, m.LeagueId, m.League })
                .ToList();

            // Takım başına geçmiş/gelecek sayıları (tek tarama).
            var past = new Dictionary<int, int>();
            var future = new Dictionary<int, int>();
            static void Inc(Dictionary<int, int> d, int id) => d[id] = d.TryGetValue(id, out var c) ? c + 1 : 1;
            foreach (var m in matches)
            {
                var isPast = m.MatchDate < now;
                if (isPast) { Inc(past, m.HomeTeamId); Inc(past, m.AwayTeamId); }
                else        { Inc(future, m.HomeTeamId); Inc(future, m.AwayTeamId); }
            }

            int P(int id) => past.TryGetValue(id, out var c) ? c : 0;
            int F(int id) => future.TryGetValue(id, out var c) ? c : 0;

            // ── Global dashboard ──
            int total = teams.Count;
            int synced = 0, coldPending = 0, built = 0, failed = 0;
            long sumPast = 0, sumFuture = 0, sumPastBuilt = 0, sumFutureBuilt = 0;
            int maxPast = 0, maxFuture = 0, histOk = 0, futOk = 0;
            DateTime? lastSync = null;

            foreach (var t in teams)
            {
                int p = P(t.Id), f = F(t.Id);
                sumPast += p; sumFuture += f;
                if (p > maxPast) maxPast = p;
                if (f > maxFuture) maxFuture = f;
                if (p >= target) histOk++;
                if (f >= 1) futOk++;

                bool isSynced = t.TimelineSyncedAt != null;
                if (isSynced)
                {
                    synced++;
                    if (t.TimelineSyncedAt > (lastSync ?? DateTime.MinValue)) lastSync = t.TimelineSyncedAt;
                    if (p + f > 0) { built++; sumPastBuilt += p; sumFutureBuilt += f; }
                    else failed++;
                }
                else coldPending++;
            }

            int Pct(int n) => total > 0 ? (int)Math.Round(100.0 * n / total) : 0;
            double Avg(long s, int n) => n > 0 ? Math.Round((double)s / n, 2) : 0;

            var dashboard = new TimelineDashboard
            {
                TotalTeams = total,
                TimelineBuiltTeams = built,
                TimelineSyncedTeams = synced,
                ColdStartPending = coldPending,
                IncrementalTracked = synced,
                FailedTimelineTeams = failed,
                AvgPastMatchesPerTeam = Avg(sumPast, total),
                AvgFutureMatchesPerTeam = Avg(sumFuture, total),
                AvgPastMatchesPerBuiltTeam = Avg(sumPastBuilt, built),
                AvgFutureMatchesPerBuiltTeam = Avg(sumFutureBuilt, built),
                MaxPastMatches = maxPast,
                MaxFutureMatches = maxFuture,
                TimelineCoveragePercent = Pct(built),
                HistoricalCoveragePercent = Pct(histOk),
                FutureCoveragePercent = Pct(futOk),
                HistoricalTargetMatches = target,
                LastSyncAtUtc = lastSync?.ToString("u"),
                LastCycleAtUtc = _telemetry.LastCycleAtUtc?.ToString("u"),
                LastCycleDurationMs = _telemetry.LastCycleDurationMs,
                LastCycleTeams = _telemetry.LastCycleTeams,
                LastCycleMatchesAdded = _telemetry.LastCycleMatchesAdded,
                ApiFailedRequests = _metrics.Snapshot().FailedRequests,
                ComputedAtUtc = now.ToString("u")
            };

            // ── Lig bazında rapor ──
            var byLeague = new Dictionary<int, LeagueAgg>();
            foreach (var m in matches)
            {
                if (!byLeague.TryGetValue(m.LeagueId, out var la))
                    byLeague[m.LeagueId] = la = new LeagueAgg { LeagueId = m.LeagueId, LeagueName = m.League };
                la.MatchCount++;
                la.TeamIds.Add(m.HomeTeamId);
                la.TeamIds.Add(m.AwayTeamId);
            }

            var syncedById = teams.ToDictionary(t => t.Id, t => t.TimelineSyncedAt);
            var leagues = new List<LeagueTimelineCoverage>(byLeague.Count);
            foreach (var la in byLeague.Values)
            {
                int tc = la.TeamIds.Count;
                int lBuilt = 0, lCold = 0, lHist = 0, lFut = 0;
                long lSumPast = 0, lSumFut = 0;
                DateTime? lLast = null;
                foreach (var id in la.TeamIds)
                {
                    int p = P(id), f = F(id);
                    lSumPast += p; lSumFut += f;
                    if (p >= target) lHist++;
                    if (f >= 1) lFut++;
                    var s = syncedById.TryGetValue(id, out var sv) ? sv : null;
                    if (s != null)
                    {
                        if (s > (lLast ?? DateTime.MinValue)) lLast = s;
                        if (p + f > 0) lBuilt++;
                    }
                    else lCold++;
                }
                int lCov = tc > 0 ? (int)Math.Round(100.0 * lBuilt / tc) : 0;
                string quality = tc == 0 ? "Empty"
                    : lCov >= 80 ? "Ready"
                    : lCov >= 40 ? "Partial"
                    : lBuilt > 0 ? "Limited"
                    : "Cold";

                leagues.Add(new LeagueTimelineCoverage
                {
                    LeagueId = la.LeagueId,
                    LeagueName = la.LeagueName,
                    TeamCount = tc,
                    TimelineBuiltTeams = lBuilt,
                    ColdStartPending = lCold,
                    AvgPastMatches = tc > 0 ? Math.Round((double)lSumPast / tc, 2) : 0,
                    AvgFutureMatches = tc > 0 ? Math.Round((double)lSumFut / tc, 2) : 0,
                    CoveragePercent = lCov,
                    HistoricalPercent = tc > 0 ? (int)Math.Round(100.0 * lHist / tc) : 0,
                    FuturePercent = tc > 0 ? (int)Math.Round(100.0 * lFut / tc) : 0,
                    LastUpdateUtc = lLast?.ToString("u"),
                    DataQuality = quality,
                    MatchCount = la.MatchCount
                });
            }

            var orderedLeagues = leagues
                .OrderByDescending(l => l.CoveragePercent)
                .ThenByDescending(l => l.MatchCount)
                .ToList();

            return new CoreSnapshot
            {
                Dashboard = dashboard,
                Leagues = orderedLeagues,
                ColdStartPending = coldPending,
                FromCache = false
            };
        }

        // ══════════════════════════════ Public API ══════════════════════════════
        public TimelineDashboard GetDashboard()
        {
            var core = Core();
            return core.Dashboard with { FromCache = core.FromCache };
        }

        public IReadOnlyList<LeagueTimelineCoverage> GetLeagues() => Core().Leagues;

        public async Task<TimelineQuotaReport> GetQuotaReportAsync(CancellationToken ct = default)
        {
            var snap = _metrics.Snapshot();
            var coldPending = Core().ColdStartPending;
            var refreshHours = RefreshHours();

            // Gerçek plan limiti — /status (cached 1h); alınamazsa config fallback.
            var status = await _provider.GetApiStatusAsync(ct);
            int limit; int usedToday; string source;
            if (status != null && status.DailyLimit > 0)
            {
                limit = status.DailyLimit; usedToday = status.RequestsToday; source = "api-football/status";
            }
            else
            {
                limit = DailyLimitConfig(); usedToday = 0; source = "config";
            }

            const int perTeam = 2; // last + next
            int safeDaily = (int)Math.Round(limit * 0.8);
            // Sürdürülebilir takım: her takım RefreshInterval'de bir yenilenir → günlük maliyet/takım = 2 × 24 / saat.
            double dailyCostPerTeam = perTeam * 24.0 / refreshHours;
            int manageable = dailyCostPerTeam > 0 ? (int)Math.Floor(safeDaily / dailyCostPerTeam) : 0;

            string verdict = manageable >= Core().Dashboard.TotalTeams
                ? $"Mevcut plan tüm {Core().Dashboard.TotalTeams} takımı rahatlıkla yönetir (~{manageable} kapasite)."
                : $"Mevcut plan güvenli limitte ~{manageable} takımı sürdürebilir (toplam {Core().Dashboard.TotalTeams}).";

            return new TimelineQuotaReport
            {
                TotalRequests = snap.TotalRequests,
                FailedRequests = snap.FailedRequests,
                CacheHits = snap.CacheHits,
                CacheGainPercent = snap.CacheGainPercent,
                RequestsPerHour = snap.RequestsPerHour,
                RequestsPerDayProjected = snap.RequestsPerDayProjected,
                RequestsByEndpoint = snap.ByEndpoint,
                JobAttributedRequests = snap.JobAttributedRequests,
                RequestsByJob = snap.ByJob,
                RequestsByJobAndEndpoint = snap.ByJobAndEndpoint,
                RequestsPerTeamTimeline = perTeam,
                ColdStartCostPerTeam = perTeam,
                IncrementalCostPerTeamRefresh = perTeam,
                ColdStartPendingTeams = coldPending,
                ColdStartRemainingRequests = coldPending * perTeam,
                DailyRequestLimit = limit,
                DailyRequestsUsedToday = usedToday,
                DailyLimitSource = source,
                RefreshIntervalHours = refreshHours,
                SafeDailyBudget = safeDaily,
                ManageableTeams = manageable,
                CapacityVerdict = verdict,
                ComputedAtUtc = DateTime.UtcNow.ToString("u")
            };
        }

        public ColdStartPreview GetColdStartPreview(int count)
        {
            count = Math.Clamp(count, 1, 500);
            var now = DateTime.UtcNow;
            var refreshHours = RefreshHours();
            var refreshCutoff = now.AddHours(-refreshHours);
            var mvp = CoveragePolicy.LeagueAllowList(_config);
            var followed = _db.UserLeagueFollows.Where(f => f.IsActive)
                .Select(f => f.LeagueId).Distinct().ToHashSet();

            var ranked = TimelinePriority.RankEligible(_db, now, refreshCutoff, mvp, followed);

            var tierNames = new Dictionary<int, string>
            {
                [1] = "1-Bugün", [2] = "2-7 gün içinde", [3] = "3-MVP lig",
                [4] = "4-Takip lig", [5] = "5-Diğer aktif", [6] = "6-Arşiv"
            };
            var breakdown = ranked.GroupBy(r => r.Tier)
                .OrderBy(g => g.Key)
                .ToDictionary(g => tierNames.TryGetValue(g.Key, out var n) ? n : g.Key.ToString(), g => g.Count());

            var batch = ranked.Take(count).Select(r => new ColdStartCandidate
            {
                TeamId = r.Id,
                ExternalTeamId = r.ExternalTeamId,
                TeamName = r.Name,
                PriorityTier = r.Tier,
                PriorityReason = r.Reason,
                NearestMatchUtc = r.NearestMatch?.ToString("u"),
                TimelineSyncedAtUtc = r.SyncedAt?.ToString("u")
            }).ToList();

            return new ColdStartPreview
            {
                MaxTeamsPerCycle = MaxTeamsPerCycle(),
                RefreshIntervalHours = refreshHours,
                EligibleTeams = ranked.Count,
                TierBreakdown = breakdown,
                NextBatch = batch,
                ComputedAtUtc = now.ToString("u")
            };
        }

        // ── internal aggregation helpers ──
        private sealed record CoreSnapshot
        {
            public TimelineDashboard Dashboard { get; init; } = new();
            public List<LeagueTimelineCoverage> Leagues { get; init; } = new();
            public int ColdStartPending { get; init; }
            public bool FromCache { get; init; }
        }

        private sealed class LeagueAgg
        {
            public int LeagueId { get; set; }
            public string LeagueName { get; set; } = "";
            public int MatchCount { get; set; }
            public HashSet<int> TeamIds { get; } = new();
        }
    }
}
