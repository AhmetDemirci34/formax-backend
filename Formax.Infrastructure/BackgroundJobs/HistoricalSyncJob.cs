using Formax.Application.DTOs.Fixtures;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Historical match backfill (Sprint 19B).
    ///
    /// Why this exists:
    ///   TheSportsDB fixture sync brings FUTURE fixtures for niche leagues but no
    ///   past results, so BuildTeamComparison finds nothing → "Yeterli veri oluşmadı".
    ///   This job pulls each team's last finished matches (eventslast.php) and feeds
    ///   them through the SAME upsert path FixtureSync uses (dedup + scored Finished).
    ///
    /// Design:
    ///   - SEPARATE job, SEPARATE concern. FixtureSyncJob is untouched.
    ///   - Reuses IFixtureSyncRepository (GetTeams/Matches ByExternalIds, AddMatch).
    ///   - Reuses SportsFixtureResult (no new DTO).
    ///   - Cadence: daily-ish (12 h). Past results change rarely; an aggressive loop
    ///     would only waste the free-tier request budget. Per-team cache (12 h TTL)
    ///     makes repeated runs near-free.
    ///   - Priority: teams with ZERO past finished matches first (the fallback cases).
    ///
    /// Dedup / safety:
    ///   - eventslast returns a match for BOTH teams that played it; ExternalMatchId
    ///     dedup (GetMatchesByExternalIds + in-batch set) keeps it single.
    ///   - Score is written ONLY for Finished (live engine owns live scores — same
    ///     rule as FixtureSync / Locked Decision #6).
    /// </summary>
    public sealed class HistoricalSyncJob : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan LoopDelay    = TimeSpan.FromHours(12);

        // Per-cycle cap so one run never blows the free-tier budget.
        private const int MaxTeamsPerCycle = 60;

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
            _logger.LogInformation("[HIST SYNC] Job started.");
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
                    _logger.LogError(ex, "[HIST SYNC] Cycle failed — retry in {Delay}.", LoopDelay);
                }

                await Task.Delay(LoopDelay, stoppingToken);
            }

            _logger.LogInformation("[HIST SYNC] Job stopped.");
        }

        /// <summary>
        /// Public for manual/admin trigger (test). The 12 h schedule above remains
        /// the only automatic trigger.
        /// </summary>
        public async Task<int> RunCycleAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var db       = sp.GetRequiredService<FormaxDbContext>();
            var provider = sp.GetRequiredService<ISportsDataProvider>();
            var repo     = sp.GetRequiredService<IFixtureSyncRepository>();
            var statsRepo = sp.GetRequiredService<IMatchLiveStatsRepository>();

            var nowThreshold = DateTime.UtcNow.AddMinutes(-105);

            // Teams with a real external id, prioritising those with NO past matches.
            var teams = await db.Teams
                .Where(t => t.ExternalTeamId != null && t.ExternalTeamId != "")
                .Select(t => new
                {
                    t.Id,
                    t.ExternalTeamId,
                    PastCount = db.Matches.Count(m =>
                        (m.HomeTeamId == t.Id || m.AwayTeamId == t.Id) && m.MatchDate < nowThreshold)
                })
                .OrderBy(t => t.PastCount)   // 0-past teams first
                .Take(MaxTeamsPerCycle)
                .ToListAsync(ct);

            if (teams.Count == 0)
            {
                _logger.LogDebug("[HIST SYNC] No external teams to backfill.");
                return 0;
            }

            // ── 1. Fetch each team's recent results (cached per team) ──────────────
            var allFixtures = new List<Application.DTOs.Fixtures.SportsFixtureResult>();
            foreach (var t in teams)
            {
                var results = await provider.GetTeamRecentResultsAsync(t.ExternalTeamId!, ct);
                allFixtures.AddRange(results);
            }

            if (allFixtures.Count == 0)
            {
                _logger.LogInformation("[HIST SYNC] {Teams} team(s) checked, no results returned.", teams.Count);
                return 0;
            }

            // ── 2. Team upsert (reuse FixtureSync dedup) ───────────────────────────
            var teamExtIds = allFixtures
                .SelectMany(f => new[] { f.HomeTeamExternalId, f.AwayTeamExternalId })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct().ToList();

            var existingTeams = repo.GetTeamsByExternalIds(teamExtIds);
            foreach (var extId in teamExtIds)
            {
                var refFix = allFixtures.First(f =>
                    f.HomeTeamExternalId == extId || f.AwayTeamExternalId == extId);
                var name = refFix.HomeTeamExternalId == extId ? refFix.HomeTeamName : refFix.AwayTeamName;
                var logo = refFix.HomeTeamExternalId == extId ? refFix.HomeLogoUrl : refFix.AwayLogoUrl;

                if (!existingTeams.ContainsKey(extId))
                {
                    repo.AddTeam(new Team
                    {
                        Name = name, ExternalTeamId = extId, LogoUrl = logo, CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await repo.SaveChangesAsync(ct);

            var teamMap = repo.GetTeamsByExternalIds(teamExtIds);

            // ── 3. Match upsert (ExternalMatchId dedup, Finished score) ────────────
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
                if (!seen.Add(f.ExternalMatchId)) continue;            // in-batch dedup
                if (existingMatches.ContainsKey(f.ExternalMatchId)) continue; // already in DB

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

            // ── 4. Mirror finished scores to MatchLiveStats (P0.2 dual-score) ──────
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

            // ── 5. Team Intelligence Enrichment ──────────────────────────────────
            // Reuses allFixtures (already in memory — zero extra API/HTTP calls).
            // Groups fixtures by external team id, computes AvgGoalsFor,
            // AvgGoalsAgainst, and IsStableTeam, then writes to Team entity.
            const int StableWindow         = 10;
            const int StableLossThreshold  = 3;
            const int MinMatchesForStable  = 5;

            // Build lookup: externalTeamId → finished fixtures involving that team
            // Each fixture appears twice (once per side), so we handle both roles.
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

            int enriched = 0;

            foreach (var (extId, team) in teamMap)
            {
                if (!fixturesByTeam.TryGetValue(extId, out var tf) || tf.Count == 0)
                    continue;

                // Goals scored / conceded per match from this team's perspective
                var goalsFor     = new List<int>(tf.Count);
                var goalsAgainst = new List<int>(tf.Count);

                foreach (var f in tf)
                {
                    bool isHome = string.Equals(
                        f.HomeTeamExternalId, extId, StringComparison.OrdinalIgnoreCase);

                    goalsFor.Add(isHome     ? f.HomeScore!.Value : f.AwayScore!.Value);
                    goalsAgainst.Add(isHome ? f.AwayScore!.Value : f.HomeScore!.Value);
                }

                team.AvgGoalsFor     = Math.Round(goalsFor.Average(),     2);
                team.AvgGoalsAgainst = Math.Round(goalsAgainst.Average(), 2);

                // IsStableTeam: last StableWindow matches, losses <= StableLossThreshold
                var last10 = tf
                    .OrderByDescending(f => f.MatchDate)
                    .Take(StableWindow)
                    .ToList();

                if (last10.Count >= MinMatchesForStable)
                {
                    int losses = last10.Count(f =>
                    {
                        bool isHome = string.Equals(
                            f.HomeTeamExternalId, extId, StringComparison.OrdinalIgnoreCase);
                        return isHome
                            ? f.HomeScore!.Value < f.AwayScore!.Value
                            : f.AwayScore!.Value < f.HomeScore!.Value;
                    });

                    team.IsStableTeam = losses <= StableLossThreshold;
                }
                // fewer than MinMatchesForStable → leave IsStableTeam null (no data)

                enriched++;
            }

            if (enriched > 0) await repo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[HIST SYNC] {Teams} team(s) → {Added} new match(es), {Scored} scored, {Enriched} team(s) enriched.",
                teams.Count, added, finishedScore.Count, enriched);

            return added;
        }
    }
}
