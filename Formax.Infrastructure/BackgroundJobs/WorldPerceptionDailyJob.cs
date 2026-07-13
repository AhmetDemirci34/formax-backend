using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.World;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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
                    await RefreshCompetitionContextsAsync(stoppingToken);

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

        // ── Competition context refresh ────────────────────────────────────────

        private async Task RefreshCompetitionContextsAsync(CancellationToken ct)
        {
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

                var mapped = matches
                    .Where(m => !string.IsNullOrWhiteSpace(m.ExternalMatchId))
                    .ToList();

                if (mapped.Count == 0)
                {
                    _logger.LogDebug("[WORLD BATCH] no matches with ExternalMatchId in window — skipping context refresh");
                    return;
                }

                foreach (var match in mapped)
                {
                    try
                    {
                        var sportsContext = await provider.GetCompetitionContextAsync(
                            match.ExternalMatchId!, ct);

                        if (sportsContext == null)
                            continue;

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
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "[WORLD BATCH] context fetch error for match {MatchId}", match.Id);
                    }
                }

                _logger.LogInformation(
                    "[WORLD BATCH] competition context refresh done — {Count} match(es) processed", mapped.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORLD BATCH] RefreshCompetitionContextsAsync failed");
            }
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
