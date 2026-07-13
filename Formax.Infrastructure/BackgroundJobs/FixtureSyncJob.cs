using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs;

/// <summary>
/// Sprint 0 — Fixture sync background job.
///
/// Cadence   : every 6 hours.
/// Window    : yesterday → today + 7 days.
/// Stagger   : 30 s startup delay to avoid boot-time DB hammering.
///
/// Cycle flow:
///   0. Acquire distributed lock — only one instance runs per cycle.
///   1. Fetch all fixtures in window from ISportsDataProvider (1 HTTP request)
///   2. Collect all unique external team IDs from the response
///   3. Batch-load existing teams (1 DB query)
///   4. Upsert teams: add new / update name + logo if changed
///   5. SaveChanges for teams → EF assigns IDs to new rows
///   6. Build externalTeamId → internal Id map
///   7. Batch-load existing matches (1 DB query)
///   8. Upsert matches: add new / update date + status + league if existing
///   9. SaveChanges for matches
///  10. Heartbeat lock so peers know this instance is still alive.
///
/// Total DB round-trips per cycle: 4 (2 reads + 2 writes) + 2 lock ops.
/// Total HTTP requests per cycle : 1.
///
/// Error policy: full cycle failure is caught and logged; job keeps running.
///              Lock is released on graceful shutdown so peers can take over
///              immediately instead of waiting for the staleness timeout.
/// Provider failure returns empty list — no side effects.
/// </summary>
public sealed class FixtureSyncJob : BackgroundService
{
    private static readonly TimeSpan StartupDelay   = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LoopDelay      = TimeSpan.FromHours(6);

    /// <summary>
    /// Lock staleness threshold.  If HeartbeatAt is older than this the lock
    /// is considered abandoned and another instance may take it.
    /// Set to 30 minutes — well above the 6-hour cycle; a missed heartbeat
    /// after a crash is detectable within half an hour.
    /// </summary>
    private static readonly TimeSpan LockStaleness  = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory      _scopeFactory;
    private readonly ILogger<FixtureSyncJob>   _logger;

    /// <summary>
    /// Unique id for this process instance: hostname + PID + random guid.
    /// Guarantees uniqueness even when multiple instances run on the same host.
    /// </summary>
    private readonly string _instanceId =
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public FixtureSyncJob(
        IServiceScopeFactory    scopeFactory,
        ILogger<FixtureSyncJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[FIXTURE SYNC] Job started — instance {InstanceId}.", _instanceId);

        await Task.Delay(StartupDelay, stoppingToken);

        try
        {
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
                    _logger.LogError(ex, "[FIXTURE SYNC] Cycle failed — will retry in {Delay}.", LoopDelay);
                }

                await Task.Delay(LoopDelay, stoppingToken);
            }
        }
        finally
        {
            // Best-effort release on graceful shutdown so peers can take over
            // immediately rather than waiting for the staleness timeout.
            await TryReleaseLockAsync();
        }

        _logger.LogInformation("[FIXTURE SYNC] Job stopped.");
    }

    // ──────────────────────────────────────────────────────────────────────────

    private async Task TryReleaseLockAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var lockRepo = scope.ServiceProvider
                .GetRequiredService<IFixtureSyncLockRepository>();
            await lockRepo.ReleaseAsync(_instanceId);
            _logger.LogInformation(
                "[FIXTURE SYNC] Lock released — instance {InstanceId}.", _instanceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[FIXTURE SYNC] Could not release lock on shutdown — will expire after {Staleness} min.",
                LockStaleness.TotalMinutes);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var lockRepo  = sp.GetRequiredService<IFixtureSyncLockRepository>();
        var provider  = sp.GetRequiredService<ISportsDataProvider>();
        var repo      = sp.GetRequiredService<IFixtureSyncRepository>();
        var statsRepo = sp.GetRequiredService<IMatchLiveStatsRepository>();

        // ── 0. Distributed lock ───────────────────────────────────────────────
        if (!await lockRepo.TryAcquireAsync(_instanceId, LockStaleness, ct))
        {
            _logger.LogDebug(
                "[FIXTURE SYNC] Lock held by another instance — skipping cycle.");
            return;
        }

        var fromDate = DateTime.UtcNow.Date.AddDays(-1);   // yesterday — status reconciliation
        var toDate   = DateTime.UtcNow.Date.AddDays(7);    // today + 7

        // ── 1. Fetch ─────────────────────────────────────────────────────────
        var fixtures = await provider.GetFixturesAsync(fromDate, toDate, ct);

        if (fixtures.Count == 0)
        {
            _logger.LogDebug("[FIXTURE SYNC] No fixtures returned for window {From}→{To}.", fromDate, toDate);
            return;
        }

        _logger.LogInformation(
            "[FIXTURE SYNC] Fetched {Count} fixture(s) for {From}→{To}.",
            fixtures.Count, fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"));

        // ── 2. Collect unique external team IDs ──────────────────────────────
        var allExternalTeamIds = fixtures
            .SelectMany(f => new[] { f.HomeTeamExternalId, f.AwayTeamExternalId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        // ── 3. Batch-load existing teams (1 query) ───────────────────────────
        var existingTeams = repo.GetTeamsByExternalIds(allExternalTeamIds);

        int teamsAdded   = 0;
        int teamsUpdated = 0;

        // ── 4. Upsert teams ──────────────────────────────────────────────────
        foreach (var externalId in allExternalTeamIds)
        {
            // Determine name + logo from first fixture that references this team
            var refFixture = fixtures.FirstOrDefault(f =>
                f.HomeTeamExternalId == externalId || f.AwayTeamExternalId == externalId);
            if (refFixture == null) continue;

            var name    = refFixture.HomeTeamExternalId == externalId
                          ? refFixture.HomeTeamName
                          : refFixture.AwayTeamName;
            var logoUrl = refFixture.HomeTeamExternalId == externalId
                          ? refFixture.HomeLogoUrl
                          : refFixture.AwayLogoUrl;

            if (existingTeams.TryGetValue(externalId, out var existing))
            {
                // Update name / logo if changed — avoids unnecessary dirty writes
                if (existing.Name != name || existing.LogoUrl != logoUrl)
                {
                    existing.Name    = name;
                    existing.LogoUrl = logoUrl;
                    teamsUpdated++;
                }
            }
            else
            {
                repo.AddTeam(new Team
                {
                    Name           = name,
                    ExternalTeamId = externalId,
                    LogoUrl        = logoUrl,
                    CreatedAt      = DateTime.UtcNow
                });
                teamsAdded++;
            }
        }

        // ── 5. Save teams (new rows get DB-generated IDs) ────────────────────
        await repo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[FIXTURE SYNC] Teams — added {Added}, updated {Updated}.",
            teamsAdded, teamsUpdated);

        // ── 6. Rebuild team ID map (covers newly inserted rows) ──────────────
        var teamMap = repo.GetTeamsByExternalIds(allExternalTeamIds);

        // ── 7. Batch-load existing matches (1 query) ─────────────────────────
        var allExternalMatchIds = fixtures
            .Select(f => f.ExternalMatchId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        var existingMatches = repo.GetMatchesByExternalIds(allExternalMatchIds);

        int matchesAdded   = 0;
        int matchesUpdated = 0;
        int matchesSkipped = 0;

        // Collected finished matches whose score must be mirrored to MatchLiveStats
        // after SaveChanges (so new rows have DB-assigned IDs).
        var finishedScoreMatches = new List<(Match match, int home, int away)>();

        // ── 8. Upsert matches ────────────────────────────────────────────────
        foreach (var fixture in fixtures)
        {
            if (string.IsNullOrWhiteSpace(fixture.ExternalMatchId)) continue;

            // Resolve internal team IDs — skip if either team cannot be resolved
            if (!teamMap.TryGetValue(fixture.HomeTeamExternalId, out var homeTeam) ||
                !teamMap.TryGetValue(fixture.AwayTeamExternalId, out var awayTeam))
            {
                _logger.LogWarning(
                    "[FIXTURE SYNC] Skipping fixture {FixtureId} — unresolved team(s) " +
                    "home={HomeExtId} away={AwayExtId}.",
                    fixture.ExternalMatchId,
                    fixture.HomeTeamExternalId,
                    fixture.AwayTeamExternalId);
                matchesSkipped++;
                continue;
            }

            // Finished fixtures carry a real final score from the provider.
            // Live scores are owned exclusively by the live engine (Locked
            // Decision #6) — FixtureSync never writes them. NotStarted = 0.
            var isFinished = fixture.Status == "Finished"
                             && fixture.HomeScore.HasValue
                             && fixture.AwayScore.HasValue;

            if (existingMatches.TryGetValue(fixture.ExternalMatchId, out var existingMatch))
            {
                // Update mutable fields only.
                existingMatch.MatchDate   = fixture.MatchDate;
                existingMatch.Status      = fixture.Status;
                existingMatch.League      = fixture.LeagueName;
                existingMatch.LeagueId    = fixture.LeagueExternalId;
                existingMatch.HomeTeamId  = homeTeam.Id;
                existingMatch.AwayTeamId  = awayTeam.Id;
                // Update referee/venue if provider supplies them (never clear existing)
                if (!string.IsNullOrWhiteSpace(fixture.Referee))  existingMatch.Referee = fixture.Referee;
                if (!string.IsNullOrWhiteSpace(fixture.Venue))    existingMatch.Venue   = fixture.Venue;
                // Finished ONLY — never touch Live/NotStarted scores.
                if (isFinished)
                {
                    existingMatch.HomeScore = fixture.HomeScore!.Value;
                    existingMatch.AwayScore = fixture.AwayScore!.Value;
                    finishedScoreMatches.Add((existingMatch, fixture.HomeScore.Value, fixture.AwayScore.Value));
                }
                matchesUpdated++;
            }
            else
            {
                var newMatch = new Match
                {
                    ExternalMatchId = fixture.ExternalMatchId,
                    MatchDate       = fixture.MatchDate,
                    Status          = fixture.Status,
                    League          = fixture.LeagueName,
                    LeagueId        = fixture.LeagueExternalId,
                    HomeTeamId      = homeTeam.Id,
                    AwayTeamId      = awayTeam.Id,
                    HomeScore       = isFinished ? fixture.HomeScore!.Value : 0,
                    AwayScore       = isFinished ? fixture.AwayScore!.Value : 0,
                    Referee         = fixture.Referee,
                    Venue           = fixture.Venue,
                    CreatedAt       = DateTime.UtcNow
                };
                repo.AddMatch(newMatch);
                if (isFinished)
                    finishedScoreMatches.Add((newMatch, fixture.HomeScore!.Value, fixture.AwayScore!.Value));
                matchesAdded++;
            }
        }

        // ── 9. Save matches (new rows get DB-generated IDs) ──────────────────
        await repo.SaveChangesAsync(ct);

        // ── 9b. Dual-score sync — mirror finished scores into MatchLiveStats ──
        // MatchHeader/Verdict read score from MatchLiveStats; keep both aligned
        // (same pattern as the seed). Live matches are excluded above, so this
        // never races the live ingestion job.
        foreach (var (match, hs, aScore) in finishedScoreMatches)
        {
            var existingStats = statsRepo.GetByMatchId(match.Id);
            await statsRepo.UpsertAsync(new MatchLiveStats
            {
                MatchId   = match.Id,
                HomeScore = hs,
                AwayScore = aScore,
                Minute    = 90,
                Phase     = "FT",
                UpdatedAt = DateTime.UtcNow
            }, existingStats, ct);
        }
        if (finishedScoreMatches.Count > 0)
            await statsRepo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[FIXTURE SYNC] Matches — added {Added}, updated {Updated}, skipped {Skipped}.",
            matchesAdded, matchesUpdated, matchesSkipped);

        // ── 10. Heartbeat ────────────────────────────────────────────────────
        await lockRepo.HeartbeatAsync(_instanceId, ct);
    }
}
