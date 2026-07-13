using Formax.Application.DTOs.Live;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Live match ingestion — rebuilt architecture:
    ///
    ///  BATCH FIRST (1 request / cycle regardless of N live matches)
    ///    GET /fixtures?live=all  →  score + clock for every live fixture.
    ///
    ///  CONDITIONAL STAT FETCH (1 request / *changed* match)
    ///    GET /fixtures/statistics?fixture={id}
    ///    Only fired when the batch entry shows a score or minute difference
    ///    versus the current DB snapshot (dirty check).
    ///
    ///  THROTTLED EVENT FETCH (every 2nd cycle = 60 s)
    ///    GET /fixtures/events?fixture={id}
    ///    Events change infrequently; 60 s cadence is sufficient.
    ///
    ///  DEEP DIRTY WRITE
    ///    Even after fetching detailed stats, the DB upsert is skipped if all
    ///    tracked fields (score, possession, shots, cards, DA) are identical
    ///    to the existing row.  Momentum snapshot is written only when stats
    ///    actually changed.
    ///
    ///  DISTRIBUTED LOCK
    ///    Only one app instance runs ingestion at a time.  Lock is a singleton
    ///    row in LiveIngestionLocks acquired with an atomic SQL UPDATE.
    ///    Stale locks (HeartbeatAt older than 90 s) are evicted automatically.
    ///
    ///  REQUEST MATH — worst case (every match changes every cycle):
    ///    N matches  |  old req/min  |  new req/min
    ///       10      |      60       |     32
    ///       50      |     300       |    152
    ///      100      |     600       |    302
    ///
    ///  REQUEST MATH — typical case (~30 % of matches change per cycle):
    ///    N matches  |  new req/min (typical)
    ///       10      |    ~11
    ///       50      |    ~47
    ///      100      |    ~92
    ///
    ///  Error tolerance: per-match failures are caught individually; the cycle
    ///  continues.  A full-cycle failure is caught at the outer loop; the job
    ///  keeps polling.  Lock is released on exception so peers can take over.
    /// </summary>
    public sealed class LiveMatchIngestionJob : BackgroundService
    {
        private static readonly TimeSpan LoopDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan LockStaleness = TimeSpan.FromSeconds(90);
        private static readonly int MomentumMaxKeep = 120;

        /// <summary>Events are fetched every N cycles (N × 30 s = 60 s).</summary>
        private static readonly int EventCycleFrequency = 2;

        private readonly ILogger<LiveMatchIngestionJob> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>
        /// Unique id for this process instance: hostname + PID + random guid.
        /// Guarantees uniqueness even when multiple instances run on the same host.
        /// </summary>
        private readonly string _instanceId =
            $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

        private int _cycleCount = 0;

        public LiveMatchIngestionJob(
            ILogger<LiveMatchIngestionJob> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────────────

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "[LIVE JOB] instance {InstanceId} started", _instanceId);

            // Stagger startup so other services can initialise first.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await RunOnceAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex,
                            "[LIVE JOB] unhandled error in cycle {Cycle}", _cycleCount);
                    }
                    finally
                    {
                        _cycleCount++;
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
        }

        private async Task TryReleaseLockAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var lockRepo = scope.ServiceProvider
                    .GetRequiredService<ILiveIngestionLockRepository>();
                await lockRepo.ReleaseAsync(_instanceId);
                _logger.LogInformation(
                    "[LIVE JOB] lock released — instance {InstanceId}", _instanceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[LIVE JOB] could not release lock on shutdown — will expire after {Staleness} s",
                    LockStaleness.TotalSeconds);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Cycle entry point
        // ──────────────────────────────────────────────────────────────────────

        private async Task RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var lockRepo = sp.GetRequiredService<ILiveIngestionLockRepository>();

            // ── 1. Distributed lock ────────────────────────────────────────────
            if (!await lockRepo.TryAcquireAsync(_instanceId, LockStaleness, ct))
            {
                _logger.LogDebug(
                    "[LIVE JOB] lock held by another instance — skipping cycle {Cycle}",
                    _cycleCount);
                return;
            }

            try
            {
                bool fetchEvents = (_cycleCount % EventCycleFrequency) == 0;
                await ProcessCycleAsync(sp, fetchEvents, ct);

                // Heartbeat so other instances know we are still alive.
                await lockRepo.HeartbeatAsync(_instanceId, ct);
            }
            catch
            {
                // Release immediately on unhandled exception so a peer can recover.
                await lockRepo.ReleaseAsync(_instanceId, ct);
                throw;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Main cycle
        // ──────────────────────────────────────────────────────────────────────

        private async Task ProcessCycleAsync(
            IServiceProvider sp,
            bool fetchEvents,
            CancellationToken ct)
        {
            var provider    = sp.GetRequiredService<ISportsDataProvider>();
            var matchRepo   = sp.GetRequiredService<IMatchReadRepository>();
            var statsRepo   = sp.GetRequiredService<IMatchLiveStatsRepository>();
            var momentumRepo = sp.GetRequiredService<IMatchMomentumRepository>();
            var eventRepo   = sp.GetRequiredService<IMatchLiveEventIngestionRepository>();

            // ── 2. Single batch request — all live fixtures (1 req total) ──────
            var batchEntries = await provider.GetAllLiveFixturesAsync(ct);

            if (batchEntries.Count == 0)
            {
                _logger.LogDebug(
                    "[LIVE JOB] batch returned 0 live fixtures at {Time}", DateTime.UtcNow);
                return;
            }

            _logger.LogDebug(
                "[LIVE JOB] batch returned {Count} live fixture(s)", batchEntries.Count);

            // ── 3. Build lookup: externalId → batch entry ──────────────────────
            var batchByExternal = batchEntries.ToDictionary(e => e.ExternalMatchId);
            var externalIds     = batchByExternal.Keys.ToHashSet();

            // ── 4. Cross-reference with our tracked matches ────────────────────
            var trackedMatches = matchRepo
                .Query()
                .Where(m => m.ExternalMatchId != null
                         && externalIds.Contains(m.ExternalMatchId))
                .Select(m => new { m.Id, m.ExternalMatchId })
                .ToList();

            if (trackedMatches.Count == 0)
            {
                _logger.LogDebug("[LIVE JOB] no tracked matches found in the live batch");
                return;
            }

            _logger.LogInformation(
                "[LIVE JOB] cycle {Cycle} | tracked={Tracked} | events={FetchEvents} | {Time}",
                _cycleCount, trackedMatches.Count, fetchEvents, DateTime.UtcNow);

            // ── 5. Batch-load current DB stats for all tracked matches ──────────
            var trackedIds   = trackedMatches.Select(m => m.Id).ToHashSet();
            var currentStats = statsRepo.GetByMatchIds(trackedIds);
            var statsByMatchId = currentStats.ToDictionary(s => s.MatchId);

            var utcNow = DateTime.UtcNow;

            // ── 6. Per-match processing (failure-isolated) ─────────────────────
            foreach (var match in trackedMatches)
            {
                try
                {
                    await ProcessMatchAsync(
                        match.Id,
                        match.ExternalMatchId!,
                        batchByExternal[match.ExternalMatchId!],
                        statsByMatchId.GetValueOrDefault(match.Id),
                        provider, statsRepo, momentumRepo, eventRepo,
                        fetchEvents, utcNow, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[LIVE JOB] error processing match {MatchId} (external: {ExternalId})",
                        match.Id, match.ExternalMatchId);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Per-match processing
        // ──────────────────────────────────────────────────────────────────────

        private async Task ProcessMatchAsync(
            int matchId,
            string externalId,
            SportsLiveBatchEntry batch,
            MatchLiveStats? currentDb,
            ISportsDataProvider provider,
            IMatchLiveStatsRepository statsRepo,
            IMatchMomentumRepository momentumRepo,
            IMatchLiveEventIngestionRepository eventRepo,
            bool fetchEvents,
            DateTime utcNow,
            CancellationToken ct)
        {
            // ── 7. Coarse dirty check using batch data (score + clock) ──────────
            bool scoreOrClockChanged =
                currentDb == null
                || currentDb.HomeScore != batch.HomeScore
                || currentDb.AwayScore != batch.AwayScore
                || currentDb.Minute    != batch.Minute;

            if (scoreOrClockChanged)
            {
                // ── 8. Fetch detailed stats (1 req only for changed matches) ────
                // Pass batch so the provider skips the redundant /fixtures?id= call.
                var liveStats = await provider.GetLiveMatchStatsAsync(externalId, batch, ct);

                if (liveStats != null)
                {
                    // ── 9. Deep dirty write guard ────────────────────────────────
                    // Skip the DB upsert entirely if none of the tracked fields changed.
                    bool statsActuallyChanged =
                        currentDb == null
                        || currentDb.HomeScore             != liveStats.HomeScore
                        || currentDb.AwayScore             != liveStats.AwayScore
                        || currentDb.Minute                != liveStats.Minute
                        || currentDb.PossessionHome        != liveStats.PossessionHome
                        || currentDb.PossessionAway        != liveStats.PossessionAway
                        || currentDb.ShotsHome             != liveStats.ShotsHome
                        || currentDb.ShotsAway             != liveStats.ShotsAway
                        || currentDb.ShotsOnTargetHome     != liveStats.ShotsOnTargetHome
                        || currentDb.ShotsOnTargetAway     != liveStats.ShotsOnTargetAway
                        || currentDb.DangerousAttacksHome  != liveStats.DangerousAttacksHome
                        || currentDb.DangerousAttacksAway  != liveStats.DangerousAttacksAway
                        || currentDb.YellowHome            != liveStats.YellowHome
                        || currentDb.YellowAway            != liveStats.YellowAway
                        || currentDb.RedHome               != liveStats.RedHome
                        || currentDb.RedAway               != liveStats.RedAway;

                    if (statsActuallyChanged)
                    {
                        var entity = new MatchLiveStats
                        {
                            MatchId               = matchId,
                            HomeScore             = liveStats.HomeScore,
                            AwayScore             = liveStats.AwayScore,
                            Minute                = liveStats.Minute,
                            Phase                 = liveStats.Phase,
                            PossessionHome        = liveStats.PossessionHome,
                            PossessionAway        = liveStats.PossessionAway,
                            ShotsHome             = liveStats.ShotsHome,
                            ShotsAway             = liveStats.ShotsAway,
                            ShotsOnTargetHome     = liveStats.ShotsOnTargetHome,
                            ShotsOnTargetAway     = liveStats.ShotsOnTargetAway,
                            CornersHome           = liveStats.CornersHome,
                            CornersAway           = liveStats.CornersAway,
                            FoulsHome             = liveStats.FoulsHome,
                            FoulsAway             = liveStats.FoulsAway,
                            OffsidesHome          = liveStats.OffsidesHome,
                            OffsidesAway          = liveStats.OffsidesAway,
                            YellowHome            = liveStats.YellowHome,
                            YellowAway            = liveStats.YellowAway,
                            RedHome               = liveStats.RedHome,
                            RedAway               = liveStats.RedAway,
                            DangerousAttacksHome  = liveStats.DangerousAttacksHome,
                            DangerousAttacksAway  = liveStats.DangerousAttacksAway,
                            XgHome                = liveStats.XgHome,
                            XgAway                = liveStats.XgAway,
                            UpdatedAt             = utcNow
                        };

                        // Pass currentDb (already EF-tracked) to skip the internal SELECT.
                        await statsRepo.UpsertAsync(entity, currentDb, ct);
                        await statsRepo.SaveChangesAsync(ct);

                        // ── 10. Momentum — only written when stats changed ───────
                        var momentum = ApiFootballSportsDataProvider.DeriveMomentum(
                            liveStats, liveStats.Minute ?? 0);

                        await momentumRepo.AddAsync(new MatchMomentumSnapshot
                        {
                            Id           = Guid.NewGuid(),
                            MatchId      = matchId,
                            MinuteBucket = momentum.MinuteBucket,
                            HomePressure = momentum.HomePressure,
                            AwayPressure = momentum.AwayPressure,
                            CreatedAt    = utcNow
                        }, ct);

                        await momentumRepo.TrimAsync(matchId, MomentumMaxKeep, ct);
                        await momentumRepo.SaveChangesAsync(ct);

                        _logger.LogDebug(
                            "[LIVE JOB] stats written — match {MatchId} min={Minute} score={H}-{A}",
                            matchId, liveStats.Minute, liveStats.HomeScore, liveStats.AwayScore);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "[LIVE JOB] stats unchanged (deep check) — match {MatchId} skipped write",
                            matchId);
                    }
                }
            }

            // ── 11. Events — throttled to every 60 s (every 2nd cycle) ─────────
            if (fetchEvents)
            {
                var events = await provider.GetLiveMatchEventsAsync(externalId, ct);

                if (events.Count > 0)
                {
                    var entities = events.Select(e => new MatchLiveEvent
                    {
                        Id          = Guid.NewGuid(),
                        MatchId     = matchId,
                        EventType   = e.EventType,
                        Minute      = e.Minute,
                        Team        = e.TeamName,
                        Player      = e.PlayerName,
                        Detail      = e.Detail,
                        ImpactScore = ComputeImpact(e.EventType),
                        CreatedAt   = utcNow
                    }).ToList();

                    await eventRepo.AddNewEventsAsync(matchId, entities, ct);
                    await eventRepo.SaveChangesAsync(ct);

                    _logger.LogDebug(
                        "[LIVE JOB] events merged — match {MatchId} provider={Count}",
                        matchId, events.Count);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private static double ComputeImpact(string eventType) => eventType switch
        {
            "Goal"         => 1.0,
            "Var"          => 0.7,
            "Card"         => 0.4,
            "Substitution" => 0.1,
            _              => 0.0
        };
    }
}
