using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.World;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    public class WorldPerceptionDailyJob : BackgroundService
    {
        private readonly ILogger<WorldPerceptionDailyJob> _logger;
        private readonly WorldPerceptionCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;

        public WorldPerceptionDailyJob(
            ILogger<WorldPerceptionDailyJob> logger,
            WorldPerceptionCache cache,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _cache = cache;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                // 🔒 04:00 UTC batch
                if (now.Hour == 4)
                {
                    _logger.LogInformation(
                        "[WORLD PERCEPTION BATCH] 04:00 job çalıştı: {Time}", now);

                    // ── World perception (static for now) ─────────────────────
                    var summary = new WorldPerceptionSummary
                    {
                        Headline = "Günlük futbol gündemi yeniden değerlendirildi",
                        Description =
                            "Gece boyunca toplanan veriler ışığında maçların " +
                            "genel bağlamı yeniden şekillendi.",
                        ConfidenceLevel = "medium",
                        Source = "batch"
                    };

                    _cache.Set(summary);

                    // ── Sprint 2: standings + competition context refresh ──────
                    await RefreshStandingsAsync(stoppingToken);
                    await RefreshCompetitionContextsAsync(ct: stoppingToken);

                    // ── Phase 6: team season statistics refresh ───────────────
                    await RefreshTeamStatisticsAsync(stoppingToken);

                    // ── Phase 6 / Slice 2: match prediction refresh (AI-only) ──
                    await RefreshMatchPredictionsAsync(stoppingToken);

                    // ── Phase 6 Final: team profile refresh (coach/venue/squad/transfers) ──
                    await RefreshTeamProfilesAsync(stoppingToken);

                    // Aynı saat içinde tekrar çalışmasın
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        // ── Standings refresh ─────────────────────────────────────────────────

        /// <summary>
        /// Public for manual/admin invocation (test). The production schedule
        /// (04:00 UTC batch in ExecuteAsync) is unchanged and still the only
        /// automatic trigger.
        /// </summary>
        public async Task RefreshStandingsAsync(CancellationToken ct)
        {
            // Kota telemetrisi: bu blokta üretilen api-football istekleri bu job'a etiketlenir.
            using var _quotaScope = Formax.Infrastructure.Telemetry
                .ApiFootballCallScope.Begin(nameof(WorldPerceptionDailyJob));

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;

                var mappingRepo = sp.GetRequiredService<ILeagueExternalMappingRepository>();
                var standingRepo = sp.GetRequiredService<ILeagueStandingRepository>();
                var matchRepo = sp.GetRequiredService<IMatchReadRepository>();
                var db = sp.GetRequiredService<FormaxDbContext>();
                var provider = sp.GetRequiredService<ISportsDataProvider>();

                // Resolve which leagues to refresh.
                // Prefer explicit mappings; when none are configured, derive them
                // generically from the leagues present on synced matches
                // (Match.LeagueId already carries the provider's external league id).
                var targets = mappingRepo.GetAll()
                    .Select(m => (LeagueId: m.LeagueId, ExternalLeagueId: m.ExternalLeagueId, Season: m.SeasonYear))
                    .ToList();

                if (targets.Count == 0)
                {
                    targets = matchRepo.Query()
                        .Where(x => x.LeagueId > 0)
                        .Select(x => new { x.LeagueId, x.MatchDate })
                        .ToList()
                        .GroupBy(x => x.LeagueId)
                        .Select(g => (
                            LeagueId: g.Key,
                            ExternalLeagueId: g.Key.ToString(),
                            Season: ResolveSeasonYear(g.Max(x => x.MatchDate))))
                        .ToList();
                }

                if (targets.Count == 0)
                {
                    _logger.LogDebug("[WORLD BATCH] no leagues to refresh standings for — skipping");
                    return;
                }

                // GDP Final Coverage — config allow-list ile evreni daralt (boş = kısıtlama yok).
                var allow = CoveragePolicy.LeagueAllowList(sp.GetRequiredService<IConfiguration>());
                if (allow.Count > 0)
                    targets = targets.Where(t => CoveragePolicy.Allows(allow, t.LeagueId)).ToList();

                // REFRESH PRIORITY — kota önce yüksek-coverage liglere (öğrenilmiş skor).
                var scoreMap = sp.GetRequiredService<ILeagueCoverageService>().ScoreMap();
                targets = targets.OrderByDescending(t => scoreMap.TryGetValue(t.LeagueId, out var s) ? s : 0).ToList();

                var utcNow = DateTime.UtcNow;

                foreach (var target in targets)
                {
                    try
                    {
                        var entries = await provider.GetLeagueStandingsAsync(
                            target.ExternalLeagueId, target.Season, ct);

                        if (entries.Count == 0)
                        {
                            _logger.LogDebug(
                                "[WORLD BATCH] no standings returned for league {LeagueId} / external {ExtId}",
                                target.LeagueId, target.ExternalLeagueId);
                            continue;
                        }

                        var standings = entries.Select(e => new LeagueStanding
                        {
                            LeagueId = target.LeagueId,
                            SeasonYear = target.Season,
                            TeamId = e.TeamId,
                            TeamName = e.TeamName,
                            Position = e.Position,
                            Played = e.Played,
                            Won = e.Won,
                            Drawn = e.Drawn,
                            Lost = e.Lost,
                            GoalsFor = e.GoalsFor,
                            GoalsAgainst = e.GoalsAgainst,
                            Points = e.Points,
                            Form = e.Form,
                            UpdatedAt = utcNow
                        }).ToList();

                        await standingRepo.ReplaceAsync(
                            target.LeagueId, target.Season, standings, ct);

                        await standingRepo.SaveChangesAsync(ct);

                        // ── Bridge: standings Position → Team.LeagueRank ──────────
                        // Match by ExternalTeamId (provider team id) — NOT internal
                        // Team.Id. Standings TeamId is the provider's id, stored on
                        // Team.ExternalTeamId during fixture sync.
                        var ranked = await BridgeTeamRanksAsync(db, entries, ct);

                        _logger.LogInformation(
                            "[WORLD BATCH] standings refreshed — league {LeagueId} season {Season}: {Count} row(s), {Ranked} team rank(s) bridged",
                            target.LeagueId, target.Season, standings.Count, ranked);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "[WORLD BATCH] standings fetch error for league {LeagueId}", target.LeagueId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORLD BATCH] RefreshStandingsAsync failed");
            }
        }

        /// <summary>
        /// Writes standings Position into Team.LeagueRank, matching on the
        /// provider team id (Team.ExternalTeamId). Returns the number of teams updated.
        /// </summary>
        private static async Task<int> BridgeTeamRanksAsync(
            FormaxDbContext db,
            List<Application.DTOs.Standings.SportsStandingEntry> entries,
            CancellationToken ct)
        {
            // entry.TeamId is the provider id (int); Team stores it as ExternalTeamId (string).
            var byExternalId = entries
                .Where(e => e.TeamId > 0 && e.Position > 0)
                .ToDictionary(e => e.TeamId.ToString(), e => e.Position);

            if (byExternalId.Count == 0) return 0;

            var extIds = byExternalId.Keys.ToList();
            var teams = await db.Teams
                .Where(t => t.ExternalTeamId != null && extIds.Contains(t.ExternalTeamId))
                .ToListAsync(ct);

            var updated = 0;
            foreach (var team in teams)
            {
                if (team.ExternalTeamId != null &&
                    byExternalId.TryGetValue(team.ExternalTeamId, out var pos) &&
                    team.LeagueRank != pos)
                {
                    team.LeagueRank = pos;
                    updated++;
                }
            }

            if (updated > 0)
                await db.SaveChangesAsync(ct);

            return updated;
        }

        private static int ResolveSeasonYear(DateTime matchDate)
            => matchDate.Month >= 7 ? matchDate.Year : matchDate.Year - 1;

        // ── Phase 6: team season statistics refresh ────────────────────────────

        /// <summary>
        /// Yaklaşan maçlardaki (−1..+7g) takımların api-football sezon istatistiklerini
        /// (/teams/statistics) çeker ve TeamSeasonStatistics'e upsert eder. Kimlik: Match.LeagueId
        /// (external lig) + Team.ExternalTeamId (external takım) + sezon. Coverage yoksa provider
        /// null döner → satır yazılmaz (fake YOK). Public: admin/manuel doğrulama için.
        /// </summary>
        public async Task RefreshTeamStatisticsAsync(CancellationToken ct)
        {
            // Kota telemetrisi: bu blokta üretilen api-football istekleri bu job'a etiketlenir.
            using var _quotaScope = Formax.Infrastructure.Telemetry
                .ApiFootballCallScope.Begin(nameof(WorldPerceptionDailyJob));

            const int MaxTeamsPerRun = 120; // HTTP/kota koruması (Pro plan 7500/gün)

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;

                var matchRepo = sp.GetRequiredService<IMatchReadRepository>();
                var statRepo  = sp.GetRequiredService<ITeamSeasonStatisticRepository>();
                var provider  = sp.GetRequiredService<ISportsDataProvider>();
                var db        = sp.GetRequiredService<FormaxDbContext>();

                var utcNow = DateTime.UtcNow;
                var matches = matchRepo.GetUpcomingMatches(utcNow.AddDays(-1), utcNow.AddDays(7))
                    .Where(m => m.LeagueId > 0 && m.HomeTeamId > 0 && m.AwayTeamId > 0)
                    .OrderBy(m => m.MatchDate)
                    .ToList();

                if (matches.Count == 0)
                {
                    _logger.LogDebug("[WORLD BATCH] no upcoming matches — skipping team stats refresh");
                    return;
                }

                // GDP Final Coverage — config allow-list ile evreni daralt (boş = kısıtlama yok).
                var allow = CoveragePolicy.LeagueAllowList(sp.GetRequiredService<IConfiguration>());
                if (allow.Count > 0)
                    matches = matches.Where(m => CoveragePolicy.Allows(allow, m.LeagueId)).ToList();

                // GDP Final Evolution — REFRESH PRIORITY: kota önce yüksek-coverage liglere. MaxTeamsPerRun
                // sınırı içinde en kaliteli ligler önce sorgulanır (öğrenilmiş coverage skoruna göre).
                var scoreMap = sp.GetRequiredService<ILeagueCoverageService>().ScoreMap();
                matches = matches
                    .OrderByDescending(m => scoreMap.TryGetValue(m.LeagueId, out var s) ? s : 0)
                    .ThenBy(m => m.MatchDate)
                    .ToList();

                // İç takım id → external takım id haritası.
                var internalIds = matches
                    .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                    .Distinct()
                    .ToList();

                var extMap = (await db.Teams
                        .Where(t => internalIds.Contains(t.Id) && t.ExternalTeamId != null)
                        .Select(t => new { t.Id, t.ExternalTeamId })
                        .ToListAsync(ct))
                    .ToDictionary(t => t.Id, t => t.ExternalTeamId!);

                // Çekilecek benzersiz (external lig, sezon, external takım) hedefleri — maç sırasına göre.
                var seen = new HashSet<string>();
                var targets = new List<(int LeagueId, int Season, string TeamExt)>();
                foreach (var m in matches)
                {
                    var season = ResolveSeasonYear(m.MatchDate);
                    foreach (var internalTeamId in new[] { m.HomeTeamId, m.AwayTeamId })
                    {
                        if (!extMap.TryGetValue(internalTeamId, out var teamExt)) continue;
                        var key = $"{m.LeagueId}:{season}:{teamExt}";
                        if (seen.Add(key))
                            targets.Add((m.LeagueId, season, teamExt));
                    }
                }

                var processed = 0;
                var written = 0;
                foreach (var t in targets)
                {
                    if (processed >= MaxTeamsPerRun) break;
                    processed++;
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var stats = await provider.GetTeamSeasonStatisticsAsync(
                            t.LeagueId.ToString(), t.TeamExt, t.Season, ct);

                        if (stats == null) continue; // coverage yok → yazma

                        await statRepo.UpsertAsync(new TeamSeasonStatistic
                        {
                            LeagueId             = t.LeagueId,
                            SeasonYear           = t.Season,
                            TeamId               = stats.TeamId,
                            TeamName             = stats.TeamName,
                            PlayedTotal          = stats.PlayedTotal,
                            PlayedHome           = stats.PlayedHome,
                            PlayedAway           = stats.PlayedAway,
                            WinsTotal            = stats.WinsTotal,
                            DrawsTotal           = stats.DrawsTotal,
                            LosesTotal           = stats.LosesTotal,
                            GoalsForAvgTotal     = stats.GoalsForAvgTotal,
                            GoalsForAvgHome      = stats.GoalsForAvgHome,
                            GoalsForAvgAway      = stats.GoalsForAvgAway,
                            GoalsAgainstAvgTotal = stats.GoalsAgainstAvgTotal,
                            GoalsAgainstAvgHome  = stats.GoalsAgainstAvgHome,
                            GoalsAgainstAvgAway  = stats.GoalsAgainstAvgAway,
                            CleanSheetTotal      = stats.CleanSheetTotal,
                            FailedToScoreTotal   = stats.FailedToScoreTotal,
                            Form                 = stats.Form,
                            UpdatedAt            = utcNow
                        }, ct);
                        written++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "[WORLD BATCH] team stats fetch error league {LeagueId} team {TeamExt}", t.LeagueId, t.TeamExt);
                    }
                }

                if (written > 0)
                    await statRepo.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "[WORLD BATCH] team stats refreshed — {Processed} team(s) queried, {Written} with coverage written",
                    processed, written);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORLD BATCH] RefreshTeamStatisticsAsync failed");
            }
        }

        // ── Phase 6 Final: team profile refresh (coach/venue/squad/transfers) ──

        /// <summary>
        /// Yaklaşan maçlardaki (−1..+7g) takımların api-football profilini (coach/venue/squad/
        /// transfers) çeker ve TeamProfileSignals'e upsert eder. AI/GDP-only. Identity: internal
        /// Team.Id → Team.ExternalTeamId → provider; kayıt internal Team.Id ile saklanır.
        /// Her alt-blok bağımsız coverage-gated; hiçbiri yoksa provider null → yazma. Public: admin.
        /// </summary>
        public async Task RefreshTeamProfilesAsync(CancellationToken ct)
        {
            // Kota telemetrisi: bu blokta üretilen api-football istekleri bu job'a etiketlenir.
            using var _quotaScope = Formax.Infrastructure.Telemetry
                .ApiFootballCallScope.Begin(nameof(WorldPerceptionDailyJob));

            const int MaxTeamsPerRun = 80; // 4 istek/takım → kota koruması

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;

                var matchRepo  = sp.GetRequiredService<IMatchReadRepository>();
                var profileRepo = sp.GetRequiredService<ITeamProfileSignalRepository>();
                var provider   = sp.GetRequiredService<ISportsDataProvider>();
                var db         = sp.GetRequiredService<FormaxDbContext>();

                var utcNow = DateTime.UtcNow;
                var matches = matchRepo.GetUpcomingMatches(utcNow.AddDays(-1), utcNow.AddDays(7))
                    .Where(m => m.HomeTeamId > 0 && m.AwayTeamId > 0)
                    .OrderBy(m => m.MatchDate)
                    .ToList();

                if (matches.Count == 0)
                {
                    _logger.LogDebug("[WORLD BATCH] no upcoming matches — skipping team profile refresh");
                    return;
                }

                // GDP Final Coverage — config allow-list ile evreni daralt (boş = kısıtlama yok).
                var allow = CoveragePolicy.LeagueAllowList(sp.GetRequiredService<IConfiguration>());
                if (allow.Count > 0)
                    matches = matches.Where(m => CoveragePolicy.Allows(allow, m.LeagueId)).ToList();

                // REFRESH PRIORITY — kota önce yüksek-coverage liglere (öğrenilmiş skor).
                var scoreMap = sp.GetRequiredService<ILeagueCoverageService>().ScoreMap();
                matches = matches
                    .OrderByDescending(m => scoreMap.TryGetValue(m.LeagueId, out var s) ? s : 0)
                    .ThenBy(m => m.MatchDate)
                    .ToList();

                var internalIds = matches
                    .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                    .Distinct()
                    .ToList();

                var extMap = (await db.Teams
                        .Where(t => internalIds.Contains(t.Id) && t.ExternalTeamId != null)
                        .Select(t => new { t.Id, t.ExternalTeamId })
                        .ToListAsync(ct))
                    .ToDictionary(t => t.Id, t => t.ExternalTeamId!);

                // Maç sırasına göre benzersiz internal takım hedefleri.
                var seen = new HashSet<int>();
                var targets = new List<int>();
                foreach (var m in matches)
                foreach (var id in new[] { m.HomeTeamId, m.AwayTeamId })
                    if (extMap.ContainsKey(id) && seen.Add(id))
                        targets.Add(id);

                var processed = 0; var written = 0;
                foreach (var internalId in targets)
                {
                    if (processed >= MaxTeamsPerRun) break;
                    processed++;
                    ct.ThrowIfCancellationRequested();

                    var ext = extMap[internalId];
                    try
                    {
                        var p = await provider.GetTeamProfileAsync(ext, ct);
                        if (p == null) continue; // coverage yok → yazma

                        await profileRepo.UpsertAsync(new TeamProfileSignal
                        {
                            TeamId             = internalId,
                            ExternalTeamId     = ext,
                            HasCoach           = p.HasCoach,
                            CoachName          = p.CoachName,
                            CoachAge           = p.CoachAge,
                            HasVenue           = p.HasVenue,
                            VenueName          = p.VenueName,
                            VenueCity          = p.VenueCity,
                            VenueCapacity      = p.VenueCapacity,
                            VenueSurface       = p.VenueSurface,
                            HasSquad           = p.HasSquad,
                            SquadSize          = p.SquadSize,
                            SquadAvgAge        = p.SquadAvgAge,
                            HasTransfers       = p.HasTransfers,
                            RecentTransfersIn  = p.RecentTransfersIn,
                            RecentTransfersOut = p.RecentTransfersOut,
                            UpdatedAt          = utcNow
                        }, ct);
                        written++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "[WORLD BATCH] team profile fetch error team {TeamId} (ext {Ext})", internalId, ext);
                    }
                }

                if (written > 0)
                    await profileRepo.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "[WORLD BATCH] team profiles refreshed — {Processed} team(s) queried, {Written} with coverage written",
                    processed, written);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORLD BATCH] RefreshTeamProfilesAsync failed");
            }
        }

        // ── Phase 6 / Slice 2: match prediction refresh (AI-only) ──────────────

        /// <summary>
        /// Yaklaşan maçların (−1..+7g) api-football öngörülerini (/predictions?fixture=) çeker ve
        /// MatchPredictionSignals'e upsert eder. YALNIZ AI sinyali — kullanıcıya gösterilmez.
        /// Identity: Match.ExternalMatchId → canonical Match.Id. Coverage yoksa provider null →
        /// satır yazılmaz (fake YOK). Public: admin/manuel doğrulama için.
        /// </summary>
        public async Task RefreshMatchPredictionsAsync(CancellationToken ct)
        {
            // Kota telemetrisi: bu blokta üretilen api-football istekleri bu job'a etiketlenir.
            using var _quotaScope = Formax.Infrastructure.Telemetry
                .ApiFootballCallScope.Begin(nameof(WorldPerceptionDailyJob));

            const int MaxPerRun = 120; // HTTP/kota koruması (fixture-başına 1 istek)

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;

                var matchRepo = sp.GetRequiredService<IMatchReadRepository>();
                var predRepo  = sp.GetRequiredService<IMatchPredictionSignalRepository>();
                var provider  = sp.GetRequiredService<ISportsDataProvider>();

                // GDP Final Coverage — config allow-list ile evreni daralt (boş = kısıtlama yok).
                var allow = CoveragePolicy.LeagueAllowList(sp.GetRequiredService<IConfiguration>());
                // REFRESH PRIORITY — kota önce yüksek-coverage liglere (öğrenilmiş skor).
                var scoreMap = sp.GetRequiredService<ILeagueCoverageService>().ScoreMap();

                var utcNow = DateTime.UtcNow;
                var matches = matchRepo.GetUpcomingMatches(utcNow.AddDays(-1), utcNow.AddDays(7))
                    .Where(m => !string.IsNullOrWhiteSpace(m.ExternalMatchId))
                    .Where(m => CoveragePolicy.Allows(allow, m.LeagueId))
                    .ToList()
                    .OrderByDescending(m => scoreMap.TryGetValue(m.LeagueId, out var s) ? s : 0)
                    .ThenBy(m => m.MatchDate)
                    .Take(MaxPerRun)
                    .ToList();

                if (matches.Count == 0)
                {
                    _logger.LogDebug("[WORLD BATCH] no upcoming matches with ExternalMatchId — skipping prediction refresh");
                    return;
                }

                var written = 0;
                foreach (var m in matches)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var p = await provider.GetMatchPredictionAsync(m.ExternalMatchId!, ct);
                        if (p == null) continue; // coverage yok → yazma

                        await predRepo.UpsertAsync(new MatchPredictionSignal
                        {
                            MatchId             = m.Id,
                            ExternalMatchId     = m.ExternalMatchId!,
                            PercentHome         = p.PercentHome,
                            PercentDraw         = p.PercentDraw,
                            PercentAway         = p.PercentAway,
                            WinnerName          = p.WinnerName,
                            WinnerSide          = p.WinnerSide,
                            WinOrDraw           = p.WinOrDraw,
                            Advice              = p.Advice,
                            UnderOver           = p.UnderOver,
                            ComparisonTotalHome = p.ComparisonTotalHome,
                            ComparisonTotalAway = p.ComparisonTotalAway,
                            UpdatedAt           = utcNow
                        }, ct);
                        written++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "[WORLD BATCH] prediction fetch error for match {MatchId}", m.Id);
                    }
                }

                if (written > 0)
                    await predRepo.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "[WORLD BATCH] match predictions refreshed — {Queried} match(es) queried, {Written} with coverage written",
                    matches.Count, written);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORLD BATCH] RefreshMatchPredictionsAsync failed");
            }
        }

        // ── Competition context refresh ────────────────────────────────────────

        /// <summary>
        /// Yaklaşan maçların (−1..+7g) api-football müsabaka bağlamını (/fixtures?id → league.type/round)
        /// çeker ve canonical CompetitionContext'e upsert eder. Coverage yoksa provider null → yazılmaz
        /// (fake YOK). MaxPerRun kota korumasıdır (fixture-başına 1 istek). Public: admin/manuel tetik
        /// (04:00 batch tek otomatik tetik olarak korunur). Yazılan satır sayısını döndürür.
        /// </summary>
        public async Task<int> RefreshCompetitionContextsAsync(int maxPerRun = 120, CancellationToken ct = default)
        {
            // Kota telemetrisi: bu blokta üretilen api-football istekleri bu job'a etiketlenir.
            using var _quotaScope = Formax.Infrastructure.Telemetry
                .ApiFootballCallScope.Begin(nameof(WorldPerceptionDailyJob));

            var written = 0;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;

                var matchRepo = sp.GetRequiredService<IMatchReadRepository>();
                var contextRepo = sp.GetRequiredService<ICompetitionContextRepository>();
                var provider = sp.GetRequiredService<ISportsDataProvider>();

                var utcNow = DateTime.UtcNow;

                // Fetch context for matches ±7 days that have an external ID
                var windowStart = utcNow.AddDays(-1);
                var windowEnd = utcNow.AddDays(7);
                var matches = matchRepo.GetUpcomingMatches(windowStart, windowEnd);

                // GDP Final Coverage — config allow-list ile evreni daralt (boş = kısıtlama yok).
                var allow = CoveragePolicy.LeagueAllowList(sp.GetRequiredService<IConfiguration>());
                // REFRESH PRIORITY — kota önce yüksek-coverage liglere (öğrenilmiş skor).
                var scoreMap = sp.GetRequiredService<ILeagueCoverageService>().ScoreMap();

                var mapped = matches
                    .Where(m => !string.IsNullOrWhiteSpace(m.ExternalMatchId))
                    .Where(m => CoveragePolicy.Allows(allow, m.LeagueId))
                    .OrderByDescending(m => scoreMap.TryGetValue(m.LeagueId, out var s) ? s : 0)
                    .ThenBy(m => m.MatchDate)
                    .Take(maxPerRun) // kota koruması: tek çalıştırmada en fazla N fixture (1 istek/fixture)
                    .ToList();

                if (mapped.Count == 0)
                {
                    _logger.LogDebug("[WORLD BATCH] no matches with ExternalMatchId in window — skipping context refresh");
                    return 0;
                }

                var covered = 0;
                foreach (var match in mapped)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var sportsContext = await provider.GetCompetitionContextAsync(
                            match.ExternalMatchId!, ct);

                        if (sportsContext == null)
                            continue; // coverage yok → yazma (fake YOK)
                        covered++;

                        var (headline, summary) = BuildContextText(sportsContext, match.League);

                        var entity = new CompetitionContext
                        {
                            MatchId = match.Id,
                            CompetitionType = sportsContext.CompetitionType,
                            StageName = sportsContext.StageName,
                            ContextHeadline = headline,
                            ContextSummary = summary,
                            BracketJson = null,   // bracket support reserved for future sprint
                            UpdatedAt = utcNow
                        };

                        await contextRepo.UpsertAsync(entity, ct);
                        await contextRepo.SaveChangesAsync(ct);
                        written++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "[WORLD BATCH] context fetch error for match {MatchId}", match.Id);
                    }
                }

                _logger.LogInformation(
                    "[WORLD BATCH] competition context refresh done — {Queried} queried, {Covered} with coverage, {Written} written",
                    mapped.Count, covered, written);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORLD BATCH] RefreshCompetitionContextsAsync failed");
            }
            return written;
        }

        // ── Context text builder ──────────────────────────────────────────────

        private static (string headline, string summary) BuildContextText(
            Application.DTOs.Standings.SportsCompetitionContext ctx,
            string matchLeague)
        {
            var leagueName = string.IsNullOrWhiteSpace(ctx.LeagueName) ? matchLeague : ctx.LeagueName;
            var stage = ctx.StageName;

            string headline;
            string summary;

            switch (ctx.CompetitionType)
            {
                case "Cup":
                    headline = $"{leagueName} — {stage}";
                    summary = "Eleme maçı: bu maçtan geçemeyen tur dışı kalıyor.";
                    break;

                case "Knockout":
                    headline = $"{leagueName} — {stage}";
                    summary = "Tek maçlık eleme: her hata belirleyici olabilir.";
                    break;

                default: // "League"
                    // Try to extract round number from "Regular Season - 25" style
                    var roundText = ExtractRoundLabel(stage);
                    headline = string.IsNullOrWhiteSpace(roundText)
                        ? leagueName
                        : $"{leagueName} — {roundText}";
                    summary = "Lig maçı: her puan sıralama yarışını doğrudan etkiliyor.";
                    break;
            }

            return (headline, summary);
        }

        private static string ExtractRoundLabel(string stageName)
        {
            if (string.IsNullOrWhiteSpace(stageName)) return string.Empty;

            // "Regular Season - 25" → "Hafta 25"
            var idx = stageName.LastIndexOf('-');
            if (idx >= 0 && int.TryParse(stageName[(idx + 1)..].Trim(), out var week))
                return $"Hafta {week}";

            return stageName;
        }
    }
}
