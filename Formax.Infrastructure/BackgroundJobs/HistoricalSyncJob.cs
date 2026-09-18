using System.Diagnostics;
using Formax.Application.DTOs.Fixtures;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Coverage;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Fixture Expansion v2 — Per-team Timeline sync (was: Historical backfill, Sprint 19B).
    ///
    /// PURPOSE
    ///   Build a rich per-team fixture Timeline in GDP so downstream AI layers
    ///   (MarketProbabilityEngine, Editorial Intelligence, LLM) can read deep
    ///   past/future context. This job ONLY grows GDP's team-based coverage — it
    ///   writes no AI text, changes no engine math.
    ///
    /// WHAT IT DOES
    ///   For each eligible team it pulls, via the SAME upsert path FixtureSync uses:
    ///     - past leg   : GET /fixtures?team={id}&last={LastN}   (default 20, finished)
    ///     - future leg : GET /fixtures?team={id}&next={NextN}   (default 20, all comps)
    ///   The raw fixtures land in the Matches table — which MatchAiContextBuilder.BuildTimeline
    ///   already reads (_matchRepo.Query()). No new Timeline table, no context change:
    ///   filling Matches enriches Timeline automatically and keeps MPE hashes identical.
    ///
    /// COLD START vs INCREMENTAL (watermark = Team.TimelineSyncedAt)
    ///   - Cold Start : a team never synced (TimelineSyncedAt == null) is picked first.
    ///   - Incremental: a synced team is re-picked only after RefreshIntervalHours; the
    ///     ExternalMatchId dedup means already-stored matches are never re-inserted, and
    ///     the provider's per-team 6 h cache means near-simultaneous cycles cost 0 calls.
    ///     Recently-synced teams are skipped entirely → API quota is protected.
    ///
    /// SEPARATION OF CONCERNS
    ///   FixtureSyncJob (−1/+7 date window) is UNTOUCHED and remains the operational,
    ///   near-term updater (status/score reconciliation). This job owns only the deep,
    ///   team-based Timeline. The two never conflict: ExternalMatchId is the single key,
    ///   and finished-score writes follow the same Locked-Decision-#6 rule (live scores
    ///   are owned exclusively by the live engine).
    ///
    /// CONFIG (all optional, safe defaults)
    ///   Timeline:MaxTeamsPerCycle     (default 40)  — teams processed per cycle
    ///   Timeline:RefreshIntervalHours (default 72)  — re-sync cadence for a synced team
    ///   Timeline:LastN / Timeline:NextN (default 20)— read by the provider (1..99)
    /// </summary>
    public sealed class HistoricalSyncJob : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan LoopDelay    = TimeSpan.FromHours(12);

        private const int DefaultMaxTeamsPerCycle = 40;
        private const int DefaultRefreshHours     = 72;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<HistoricalSyncJob> _logger;

        public HistoricalSyncJob(
            IServiceScopeFactory scopeFactory,
            ILogger<HistoricalSyncJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[TIMELINE SYNC] Job started.");
            await Task.Delay(StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TIMELINE SYNC] Cycle failed — retry in {Delay}.", LoopDelay);
                }

                await Task.Delay(LoopDelay, stoppingToken);
            }

            _logger.LogInformation("[TIMELINE SYNC] Job stopped.");
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Automatic + admin cycle: pick eligible teams by watermark, sync each Timeline.
        // Returns total matches added across all processed teams.
        // ──────────────────────────────────────────────────────────────────────────
        public async Task<int> RunCycleAsync(CancellationToken ct)
        {
            // Kota telemetrisi: bu turda üretilen api-football istekleri bu job'a etiketlenir.
            using var _quotaScope = Formax.Infrastructure.Telemetry.ApiFootballCallScope.Begin(nameof(HistoricalSyncJob));

            var sw = Stopwatch.StartNew();
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var db        = sp.GetRequiredService<FormaxDbContext>();
            var config    = sp.GetRequiredService<IConfiguration>();
            var metrics   = sp.GetRequiredService<ApiFootballMetrics>();
            var telemetry = sp.GetRequiredService<TimelineSyncTelemetry>();

            // ── ABONELİK PLANI KAPISI ──────────────────────────────────────────────
            // Bu job'un İKİ ayağı da (fixtures?team=&last= / &next=) plan tarafından kapatılmışsa
            // (ölçüldü 18.09.2026: "Free plans do not have access to the Last/Next parameter.")
            // tur hiç açılmaz: istek üretmek kotayı boşa harcar ve takımı "senkronlandı" damgalamak
            // kapsamı YANLIŞ gösterir (plan reddi ≠ takımın maçı yok). Plan yükseltilirse restart
            // durumu sıfırlar ve istekler kendiliğinden yeniden denenir.
            var planState = sp.GetService<Formax.Infrastructure.Http.ApiFootballPlanState>();
            if (planState?.TeamWindowBlocked == true)
            {
                _logger.LogWarning(
                    "[TIMELINE SYNC] Abonelik planı takım penceresini kapatıyor ({Detail}) — tur atlandı, " +
                    "istek üretilmedi, watermark damgalanmadı.", planState.TeamWindowDetail ?? "plan reddi");
                return 0;
            }

            var maxTeams     = ConfigInt(config, "Timeline:MaxTeamsPerCycle", DefaultMaxTeamsPerCycle, 1, 1000);
            var refreshHours = ConfigInt(config, "Timeline:RefreshIntervalHours", DefaultRefreshHours, 1, 100000);
            var now = DateTime.UtcNow;
            var refreshCutoff = now.AddHours(-refreshHours);

            // ── Cold Start Prioritization (TEK otorite: TimelinePriority) ──────────
            // Kota asla kapsam-dışı/önemsiz takımlara önce harcanmaz: bugün → 7 gün → MVP lig
            // → takip lig → diğer aktif → arşiv. Watermark ile taze senkronlananlar elenir.
            var mvp = CoveragePolicy.LeagueAllowList(config);
            var followed = db.UserLeagueFollows.Where(f => f.IsActive)
                .Select(f => f.LeagueId).Distinct().ToHashSet();
            var ranked = TimelinePriority.RankEligible(db, now, refreshCutoff, mvp, followed);

            if (ranked.Count == 0)
            {
                _logger.LogInformation("[TIMELINE SYNC] No teams due for Timeline sync (all fresh within {Hours}h).", refreshHours);
                return 0;
            }

            var batch = ranked.Take(maxTeams).ToList();
            var tierSummary = string.Join(", ", batch.GroupBy(b => b.Tier).OrderBy(g => g.Key)
                .Select(g => $"T{g.Key}={g.Count()}"));
            _logger.LogInformation(
                "[TIMELINE SYNC] {Batch}/{Eligible} takım seçildi (öncelik: {Tiers}).",
                batch.Count, ranked.Count, tierSummary);

            var provider = sp.GetRequiredService<ISportsDataProvider>();
            var apiBefore = metrics.Snapshot().TotalRequests;

            // ── Fetch each team's Timeline: past (last=N) + future (next=N). ────────
            var allFixtures = new List<SportsFixtureResult>();
            int noData = 0;
            foreach (var r in batch)
            {
                var past   = await provider.GetTeamRecentResultsAsync(r.ExternalTeamId!, ct);
                var future = await provider.GetTeamUpcomingFixturesAsync(r.ExternalTeamId!, ct);
                if (past.Count + future.Count == 0) noData++;
                allFixtures.AddRange(past);
                allFixtures.AddRange(future);
            }

            var added = await IngestFixturesAsync(sp, allFixtures, ct);

            // Plan reddi tur ORTASINDA öğrenilmiş olabilir (ilk deneme): bu turun takımları
            // "senkronlandı" damgalanmaz, yoksa kapsam yanlış görünür ve plan yükseltildiğinde
            // bu takımlar RefreshInterval dolana kadar bir daha sorulmazdı.
            if (planState?.TeamWindowBlocked == true)
            {
                _logger.LogWarning(
                    "[TIMELINE SYNC] Abonelik planı takım penceresini kapattı ({Detail}) — watermark damgalanmadı; " +
                    "bu turda yazılan maç: {Added}.", planState.TeamWindowDetail ?? "plan reddi", added);
                return added;
            }

            // ── Stamp watermark on the processed teams (Cold Start → Incremental). ──
            // Set even when a team returned 0 fixtures (no coverage) so it is not
            // re-hammered every cycle; RefreshInterval will retry it later.
            var batchIds = batch.Select(b => b.Id).ToList();
            var teamEntities = await db.Teams.Where(t => batchIds.Contains(t.Id)).ToListAsync(ct);
            var stamp = DateTime.UtcNow;
            foreach (var t in teamEntities) t.TimelineSyncedAt = stamp;
            await db.SaveChangesAsync(ct);

            sw.Stop();
            var apiUsed = metrics.Snapshot().TotalRequests - apiBefore;
            telemetry.RecordCycle("cycle", sw.ElapsedMilliseconds, batch.Count, added, noData, apiUsed);

            _logger.LogInformation(
                "[TIMELINE SYNC] {Teams} takım senkronlandı → {Added} yeni maç, {NoData} kapsam-dışı, {Api} API isteği, {Ms} ms.",
                batch.Count, added, noData, apiUsed, sw.ElapsedMilliseconds);

            return added;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Targeted single-team sync (admin/test proof). Fetches last+next for ONE team,
        // ingests, and stamps its watermark. Returns matches added.
        // ──────────────────────────────────────────────────────────────────────────
        public async Task<int> RunForTeamAsync(string externalTeamId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(externalTeamId)) return 0;

            // Kota telemetrisi: bu turda üretilen api-football istekleri bu job'a etiketlenir.
            using var _quotaScope = Formax.Infrastructure.Telemetry.ApiFootballCallScope.Begin(nameof(HistoricalSyncJob));

            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var db       = sp.GetRequiredService<FormaxDbContext>();
            var provider = sp.GetRequiredService<ISportsDataProvider>();

            var allFixtures = new List<SportsFixtureResult>();
            allFixtures.AddRange(await provider.GetTeamRecentResultsAsync(externalTeamId, ct));
            allFixtures.AddRange(await provider.GetTeamUpcomingFixturesAsync(externalTeamId, ct));

            var added = await IngestFixturesAsync(sp, allFixtures, ct);

            // Stamp watermark on the target team if it exists in GDP.
            var team = await db.Teams.FirstOrDefaultAsync(t => t.ExternalTeamId == externalTeamId, ct);
            if (team != null)
            {
                team.TimelineSyncedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "[TIMELINE SYNC] Team {TeamId} synced → {Added} new match(es).", externalTeamId, added);
            return added;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Shared ingest: team upsert + match upsert (ExternalMatchId dedup = incremental)
        // + finished-score mirror + team goal-avg / stability enrichment. No AI text.
        // ──────────────────────────────────────────────────────────────────────────
        private async Task<int> IngestFixturesAsync(
            IServiceProvider sp, List<SportsFixtureResult> allFixtures, CancellationToken ct)
        {
            if (allFixtures.Count == 0) return 0;

            var repo      = sp.GetRequiredService<IFixtureSyncRepository>();
            var statsRepo = sp.GetRequiredService<IMatchLiveStatsRepository>();

            // ── KİLİTLİ MÜSABAKA KAPSAMI ───────────────────────────────────────────
            // Buradaki fikstürler takımın TÜM takvimidir (team?last/next): kapsam içi bir
            // takımın hazırlık maçı, rezerv/U-turnuvası veya kapsam dışı kupası da gelir.
            // Ölçüldü: canonical Matches'e giren kapsam dışı satırların ana kaynağı burasıydı
            // (13.08'de hâlâ "Friendlies Clubs", "Reserve League", "Paulista - U20" yazılıyordu).
            // Allow-list boşsa kısıtlama yoktur (geri-uyum).
            var scope = CoveragePolicy.LeagueAllowList(sp.GetRequiredService<IConfiguration>());
            if (scope.Count > 0)
            {
                var beforeScope = allFixtures.Count;
                allFixtures = allFixtures
                    .Where(f => CoveragePolicy.Allows(scope, f.LeagueExternalId))
                    .ToList();
                if (allFixtures.Count != beforeScope)
                    _logger.LogInformation(
                        "[TIMELINE SYNC] Kapsam süzgeci — {Kept}/{Before} fikstür kaldı (kapsam dışı {Dropped} yazılmadı).",
                        allFixtures.Count, beforeScope, beforeScope - allFixtures.Count);
                if (allFixtures.Count == 0) return 0;
            }

            // ── Team upsert (reuse FixtureSync dedup) ──────────────────────────────
            var teamExtIds = allFixtures
                .SelectMany(f => new[] { f.HomeTeamExternalId, f.AwayTeamExternalId })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct().ToList();

            var existingTeams = repo.GetTeamsByExternalIds(teamExtIds);
            foreach (var extId in teamExtIds)
            {
                if (existingTeams.ContainsKey(extId)) continue;
                var refFix = allFixtures.First(f =>
                    f.HomeTeamExternalId == extId || f.AwayTeamExternalId == extId);
                var name = refFix.HomeTeamExternalId == extId ? refFix.HomeTeamName : refFix.AwayTeamName;
                var logo = refFix.HomeTeamExternalId == extId ? refFix.HomeLogoUrl : refFix.AwayLogoUrl;

                repo.AddTeam(new Team
                {
                    Name = name, ExternalTeamId = extId, LogoUrl = logo, CreatedAt = DateTime.UtcNow
                });
            }
            await repo.SaveChangesAsync(ct);

            var teamMap = repo.GetTeamsByExternalIds(teamExtIds);

            // ── Match upsert (ExternalMatchId dedup → only NEW matches written) ─────
            var extMatchIds = allFixtures
                .Select(f => f.ExternalMatchId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct().ToList();

            var existingMatches = repo.GetMatchesByExternalIds(extMatchIds);
            var seen = new HashSet<string>();
            var finishedScore = new List<(Match m, int h, int a)>();
            int added = 0;

            foreach (var f in allFixtures)
            {
                if (string.IsNullOrWhiteSpace(f.ExternalMatchId)) continue;
                if (!seen.Add(f.ExternalMatchId)) continue;                 // in-batch dedup
                if (existingMatches.ContainsKey(f.ExternalMatchId)) continue; // already in DB (incremental)

                if (!teamMap.TryGetValue(f.HomeTeamExternalId, out var home) ||
                    !teamMap.TryGetValue(f.AwayTeamExternalId, out var away))
                    continue;

                var isFinished = f.Status == "Finished" && f.HomeScore.HasValue && f.AwayScore.HasValue;

                var match = new Match
                {
                    ExternalMatchId = f.ExternalMatchId,
                    MatchDate = f.MatchDate,
                    Status = f.Status,
                    League = f.LeagueName,
                    LeagueId = f.LeagueExternalId,
                    HomeTeamId = home.Id,
                    AwayTeamId = away.Id,
                    HomeScore = isFinished ? f.HomeScore!.Value : 0,
                    AwayScore = isFinished ? f.AwayScore!.Value : 0,
                    Venue = f.Venue,
                    CreatedAt = DateTime.UtcNow
                };
                repo.AddMatch(match);
                if (isFinished) finishedScore.Add((match, f.HomeScore!.Value, f.AwayScore!.Value));
                added++;
            }

            await repo.SaveChangesAsync(ct);

            // ── Mirror finished scores to MatchLiveStats (dual-score parity) ───────
            foreach (var (m, h, a) in finishedScore)
            {
                var existing = statsRepo.GetByMatchId(m.Id);
                await statsRepo.UpsertAsync(new MatchLiveStats
                {
                    MatchId = m.Id, HomeScore = h, AwayScore = a,
                    Minute = 90, Phase = "FT", UpdatedAt = DateTime.UtcNow
                }, existing, ct);
            }
            if (finishedScore.Count > 0) await statsRepo.SaveChangesAsync(ct);

            // ── Team Intelligence Enrichment (data only — AvgGoals*, IsStableTeam) ──
            // Reuses allFixtures already in memory (zero extra API calls).
            EnrichTeams(allFixtures, teamMap);
            await repo.SaveChangesAsync(ct);

            return added;
        }

        // ── Team goal-average + stability enrichment (finished fixtures only). ──────
        private static void EnrichTeams(
            List<SportsFixtureResult> allFixtures, Dictionary<string, Team> teamMap)
        {
            const int StableWindow        = 10;
            const int StableLossThreshold = 3;
            const int MinMatchesForStable = 5;

            var fixturesByTeam = new Dictionary<string, List<SportsFixtureResult>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var f in allFixtures)
            {
                if (f.Status != "Finished" || f.HomeScore == null || f.AwayScore == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(f.HomeTeamExternalId))
                {
                    if (!fixturesByTeam.TryGetValue(f.HomeTeamExternalId, out var hList))
                        fixturesByTeam[f.HomeTeamExternalId] = hList = new List<SportsFixtureResult>();
                    hList.Add(f);
                }
                if (!string.IsNullOrWhiteSpace(f.AwayTeamExternalId))
                {
                    if (!fixturesByTeam.TryGetValue(f.AwayTeamExternalId, out var aList))
                        fixturesByTeam[f.AwayTeamExternalId] = aList = new List<SportsFixtureResult>();
                    aList.Add(f);
                }
            }

            foreach (var (extId, team) in teamMap)
            {
                if (!fixturesByTeam.TryGetValue(extId, out var tf) || tf.Count == 0)
                    continue;

                var goalsFor     = new List<int>(tf.Count);
                var goalsAgainst = new List<int>(tf.Count);
                foreach (var f in tf)
                {
                    bool isHome = string.Equals(f.HomeTeamExternalId, extId, StringComparison.OrdinalIgnoreCase);
                    goalsFor.Add(isHome     ? f.HomeScore!.Value : f.AwayScore!.Value);
                    goalsAgainst.Add(isHome ? f.AwayScore!.Value : f.HomeScore!.Value);
                }

                team.AvgGoalsFor     = Math.Round(goalsFor.Average(),     2);
                team.AvgGoalsAgainst = Math.Round(goalsAgainst.Average(), 2);

                var last10 = tf.OrderByDescending(f => f.MatchDate).Take(StableWindow).ToList();
                if (last10.Count >= MinMatchesForStable)
                {
                    int losses = last10.Count(f =>
                    {
                        bool isHome = string.Equals(f.HomeTeamExternalId, extId, StringComparison.OrdinalIgnoreCase);
                        return isHome
                            ? f.HomeScore!.Value < f.AwayScore!.Value
                            : f.AwayScore!.Value < f.HomeScore!.Value;
                    });
                    team.IsStableTeam = losses <= StableLossThreshold;
                }
            }
        }

        private static int ConfigInt(IConfiguration config, string key, int fallback, int min, int max)
        {
            if (int.TryParse(config[key], out var v) && v >= min && v <= max) return v;
            return fallback;
        }
    }
}
