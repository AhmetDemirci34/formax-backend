using Formax.Application.DTOs.Live;
using Formax.Application.Interfaces;
using Formax.Application.UseCases.Follow;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
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
        private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

        /// <summary>
        /// Unique id for this process instance: hostname + PID + random guid.
        /// Guarantees uniqueness even when multiple instances run on the same host.
        /// </summary>
        private readonly string _instanceId =
            $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

        private int _cycleCount = 0;

        public LiveMatchIngestionJob(
            ILogger<LiveMatchIngestionJob> logger,
            IServiceScopeFactory scopeFactory,
            Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _config = config;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────────────

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // ── CANLI VERİ KAPALI (LiveMatchData:Enabled) ─────────────────────
            // Bu job api-football'un canlı uçlarını 30 sn'de bir yokluyordu:
            //   GET /fixtures?live=all           → döngü başına 1 istek (günde ~2.880)
            //   GET /fixtures/statistics?fixture → değişen maç başına
            //   GET /fixtures/events?fixture     → her 2. döngüde
            // Günlük kotanın (7.500) büyük kısmını bu tüketiyordu. Bayrak kapalıyken job HİÇ
            // çalışmaz — tek istek bile gitmez. Kod, tablolar ve DTO'lar OLDUĞU GİBİ durur;
            // bayrak true yapılınca özellik aynen geri gelir.
            if (!LiveMatchDataFlag.IsEnabled(_config))
            {
                _logger.LogInformation(
                    "[LIVE JOB] canlı maç verisi KAPALI (LiveMatchData:Enabled=false) — "
                    + "api-football canlı uçlarına istek atılmayacak");
                return;
            }

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
            var followUseCase   = sp.GetRequiredService<GetUsersFollowingMatchUseCase>();
            var notificationRepo = sp.GetRequiredService<IUserNotificationRepository>();
            // Bildirim fan-out'u yalnız MAÇ takipçilerine gidiyordu; takım ve lig takipçileri
            // gerçek canlı olaylarda hiç bildirim almıyordu. MatchEventNotificationService'in
            // zaten uyguladığı 3'lü hedefleme burada da kullanılır (aynı, kayıtlı repo'lar).
            var teamFollowRepo   = sp.GetRequiredService<IUserTeamFollowRepository>();
            var leagueFollowRepo = sp.GetRequiredService<IUserLeagueFollowRepository>();

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
                        followUseCase, notificationRepo,
                        matchRepo, teamFollowRepo, leagueFollowRepo,
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
            GetUsersFollowingMatchUseCase followUseCase,
            IUserNotificationRepository notificationRepo,
            IMatchReadRepository matchRepo,
            IUserTeamFollowRepository teamFollowRepo,
            IUserLeagueFollowRepository leagueFollowRepo,
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

                    var newEvents = await eventRepo.AddNewEventsAsync(matchId, entities, ct);
                    await eventRepo.SaveChangesAsync(ct);

                    _logger.LogDebug(
                        "[LIVE JOB] events merged — match {MatchId} provider={Count} new={New}",
                        matchId, events.Count, newEvents.Count);

                    // ── 12. Notification fan-out — only for genuinely NEW events ──
                    // Reuses the existing dedup (AddNewEventsAsync) so each real event
                    // notifies followers exactly once. No new engine/table introduced.
                    if (newEvents.Count > 0)
                        await FanOutEventNotificationsAsync(
                            matchId, newEvents, followUseCase, notificationRepo,
                            matchRepo, teamFollowRepo, leagueFollowRepo, utcNow, ct);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Notification fan-out (real event → followers)
        // ──────────────────────────────────────────────────────────────────────

        private async Task FanOutEventNotificationsAsync(
            int matchId,
            IReadOnlyList<MatchLiveEvent> newEvents,
            GetUsersFollowingMatchUseCase followUseCase,
            IUserNotificationRepository notificationRepo,
            IMatchReadRepository matchRepo,
            IUserTeamFollowRepository teamFollowRepo,
            IUserLeagueFollowRepository leagueFollowRepo,
            DateTime utcNow,
            CancellationToken ct)
        {
            // Only high-signal events become notifications (Goal + Red Card),
            // matching the original MatchEventService intent. Yellow/sub/var skipped.
            var notif = newEvents
                .Select(BuildNotificationContent)
                .Where(c => c != null)
                .Select(c => c!.Value)
                .ToList();

            if (notif.Count == 0)
                return;

            // Hedefleme (MatchEventNotificationService ile aynı 3'lü kural):
            //   1) maçı takip edenler, 2) iki takımdan birini takip edenler, 3) ligi takip edenler.
            // HashSet ile tekilleştirilir → bir kullanıcı olay başına tek bildirim alır.
            var userIds = new HashSet<int>(await followUseCase.ExecuteAsync(matchId));

            var match = matchRepo.GetById(matchId);
            if (match != null)
            {
                foreach (var uid in await teamFollowRepo.GetUserIdsByTeamAsync(match.HomeTeamId))
                    userIds.Add(uid);
                foreach (var uid in await teamFollowRepo.GetUserIdsByTeamAsync(match.AwayTeamId))
                    userIds.Add(uid);
                foreach (var uid in await leagueFollowRepo.GetUserIdsByLeagueAsync(match.LeagueId))
                    userIds.Add(uid);
            }

            if (userIds.Count == 0)
                return;

            foreach (var userId in userIds)
            {
                foreach (var (title, message, eventType) in notif)
                {
                    try
                    {
                        await notificationRepo.AddAsync(new UserNotification
                        {
                            UserId     = userId,
                            MatchId    = matchId,
                            Title      = title,
                            Message    = message,
                            IsRead     = false,
                            CreatedAt  = utcNow,
                            EventType  = eventType,
                            Category   = NotificationCategory.Match,
                            TargetType = NotificationTargetType.Match,
                            TargetId   = matchId
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "[LIVE JOB] could not save notification for user {UserId} / match {MatchId}",
                            userId, matchId);
                    }
                }
            }
        }

        // Maps a provider live event to notification content. Returns null to skip.
        private static (string Title, string Message, NotificationEventType EventType)? BuildNotificationContent(
            MatchLiveEvent e)
        {
            var minute = e.Minute > 0 ? $"{e.Minute}'" : string.Empty;
            var team   = string.IsNullOrWhiteSpace(e.Team) ? null : e.Team.Trim();
            var player = string.IsNullOrWhiteSpace(e.Player) ? null : e.Player.Trim();

            switch (e.EventType)
            {
                case "Goal":
                {
                    var who = player ?? team ?? "Bir takım";
                    var msg = team != null
                        ? $"{team} {minute} gol buldu." + (player != null ? $" ({player})" : string.Empty)
                        : $"{who} {minute} gol buldu.";
                    return ($"⚽ Gol!", msg.Trim(), NotificationEventType.Goal);
                }

                // Provider emits "Card" with red distinguished in Detail.
                case "Card" when e.Detail != null &&
                                 e.Detail.Contains("Red", StringComparison.OrdinalIgnoreCase):
                {
                    var msg = player != null
                        ? $"{team} takımından {player}, {minute} kırmızı kart gördü."
                        : $"{team ?? "Bir takım"} {minute} kırmızı kart gördü.";
                    return ("🟥 Kırmızı Kart", msg.Trim(), NotificationEventType.RedCard);
                }

                default:
                    return null;
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
