using Formax.Application.Interfaces;
using Formax.Application.UseCases.Follow;
using Formax.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Background job that polls the sports data provider every 5 minutes.
    ///
    /// Match window: [MatchDate - 60 min, MatchDate + 90 min] UTC.
    ///
    /// On lineup release:
    ///   1. Persists MatchLineup + MatchLineupPlayers
    ///   2. Persists MatchPlayerStatuses
    ///   3. Fan-outs UserNotification to every follower of that match
    ///   4. Calls INotificationService (webhook / future push)
    /// </summary>
    public sealed class LineupIngestionJob : BackgroundService
    {
        private static readonly TimeSpan LoopDelay = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan WindowBefore = TimeSpan.FromMinutes(60);
        private static readonly TimeSpan WindowAfter = TimeSpan.FromMinutes(90);

        private readonly ILogger<LineupIngestionJob> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public LineupIngestionJob(
            ILogger<LineupIngestionJob> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[LINEUP JOB] started");

            // Stagger startup to avoid hammering the DB during boot
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[LINEUP JOB] unhandled error in RunOnce");
                }

                await Task.Delay(LoopDelay, stoppingToken);
            }
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var matchRepo = sp.GetRequiredService<IMatchReadRepository>();
            var lineupRepo = sp.GetRequiredService<IMatchLineupRepository>();
            var statusRepo = sp.GetRequiredService<IMatchPlayerStatusRepository>();
            var sportsProvider = sp.GetRequiredService<ISportsDataProvider>();
            var followUseCase = sp.GetRequiredService<GetUsersFollowingMatchUseCase>();
            var notificationRepo = sp.GetRequiredService<IUserNotificationRepository>();
            var notificationService = sp.GetRequiredService<INotificationService>();

            var utcNow = DateTime.UtcNow;
            var windowStart = utcNow - WindowBefore;
            var windowEnd = utcNow + WindowAfter;

            var matches = matchRepo.GetUpcomingMatches(windowStart, windowEnd);

            if (matches.Count == 0)
            {
                _logger.LogDebug("[LINEUP JOB] no matches in window at {Time}", utcNow);
                return;
            }

            _logger.LogInformation("[LINEUP JOB] processing {Count} match(es) at {Time}", matches.Count, utcNow);

            foreach (var match in matches)
            {
                if (string.IsNullOrWhiteSpace(match.ExternalMatchId))
                    continue;   // not mapped to an external source yet

                await ProcessMatchAsync(
                    match, lineupRepo, statusRepo,
                    sportsProvider, followUseCase,
                    notificationRepo, notificationService,
                    utcNow, ct);
            }
        }

        private async Task ProcessMatchAsync(
            Match match,
            IMatchLineupRepository lineupRepo,
            IMatchPlayerStatusRepository statusRepo,
            ISportsDataProvider sportsProvider,
            GetUsersFollowingMatchUseCase followUseCase,
            IUserNotificationRepository notificationRepo,
            INotificationService notificationService,
            DateTime utcNow,
            CancellationToken ct)
        {
            // ── 1. Fetch lineup from provider ─────────────────────────────────

            var lineupResult = await sportsProvider.GetOfficialLineupAsync(
                match.ExternalMatchId!, ct);

            if (lineupResult == null)
            {
                _logger.LogDebug("[LINEUP JOB] no lineup data for match {MatchId}", match.Id);
            }
            else
            {
                var existing = lineupRepo.GetByMatchId(match.Id);
                bool firstRelease = existing == null
                    && lineupResult.LineupsAnnounced;
                bool wasAlreadyReleased = existing?.HomeLineupsReleased == true
                    || existing?.AwayLineupsReleased == true;

                // ── 2. Persist MatchLineup header ─────────────────────────────
                var lineup = new MatchLineup
                {
                    MatchId = match.Id,
                    HomeLineupsReleased = lineupResult.HomeStarters.Count > 0,
                    AwayLineupsReleased = lineupResult.AwayStarters.Count > 0,
                    ReleasedAt = lineupResult.LineupsAnnounced ? utcNow : null,
                    FetchedAt = utcNow
                };

                await lineupRepo.UpsertAsync(lineup, ct);

                // ── 3. Persist players ────────────────────────────────────────
                var players = new List<MatchLineupPlayer>();

                players.AddRange(MapPlayers(match.Id, "Home", "Starter", lineupResult.HomeStarters));
                players.AddRange(MapPlayers(match.Id, "Home", "Bench", lineupResult.HomeBench));
                players.AddRange(MapPlayers(match.Id, "Away", "Starter", lineupResult.AwayStarters));
                players.AddRange(MapPlayers(match.Id, "Away", "Bench", lineupResult.AwayBench));

                if (players.Count > 0)
                    await lineupRepo.ReplacePlayersAsync(match.Id, players, ct);

                await lineupRepo.SaveChangesAsync(ct);

                // ── 4. First-release event → notification fan-out ─────────────
                bool triggerNotification = firstRelease
                    || (lineupResult.LineupsAnnounced && !wasAlreadyReleased);

                if (triggerNotification)
                {
                    _logger.LogInformation(
                        "[LINEUP JOB] official_lineup_released for match {MatchId}", match.Id);

                    await FanOutNotificationAsync(
                        match, followUseCase, notificationRepo,
                        notificationService, utcNow, ct);
                }
            }

            // ── 5. Fetch + persist player statuses ────────────────────────────

            var statuses = await sportsProvider.GetPlayerStatusesAsync(
                match.ExternalMatchId!, ct);

            if (statuses.Count > 0)
            {
                var statusEntities = statuses.Select(s => new MatchPlayerStatus
                {
                    Id = Guid.NewGuid(),
                    MatchId = match.Id,
                    TeamId = s.TeamId,
                    PlayerName = s.PlayerName,
                    Status = s.Status,
                    Reason = s.Reason,
                    FetchedAt = utcNow
                }).ToList();

                await statusRepo.ReplaceAsync(match.Id, statusEntities, ct);
                await statusRepo.SaveChangesAsync(ct);

                _logger.LogDebug(
                    "[LINEUP JOB] persisted {Count} player status(es) for match {MatchId}",
                    statusEntities.Count, match.Id);
            }
        }

        private async Task FanOutNotificationAsync(
            Match match,
            GetUsersFollowingMatchUseCase followUseCase,
            IUserNotificationRepository notificationRepo,
            INotificationService notificationService,
            DateTime utcNow,
            CancellationToken ct)
        {
            var homeName = match.HomeTeam?.Name ?? $"Ev sahibi ({match.HomeTeamId})";
            var awayName = match.AwayTeam?.Name ?? $"Deplasman ({match.AwayTeamId})";

            var title = "İlk 11 Açıklandı";
            var message = $"{homeName} - {awayName} ilk 11'leri açıklandı.";

            // Per-user DB notifications
            var userIds = await followUseCase.ExecuteAsync(match.Id);

            foreach (var userId in userIds)
            {
                try
                {
                    var notification = new UserNotification
                    {
                        UserId = userId,
                        MatchId = match.Id,
                        Title = title,
                        Message = message,
                        IsRead = false,
                        CreatedAt = utcNow
                    };

                    await notificationRepo.AddAsync(notification);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[LINEUP JOB] could not save notification for user {UserId} / match {MatchId}",
                        userId, match.Id);
                }
            }

            // Webhook / future push (NullNotificationService in current config)
            try
            {
                await notificationService.NotifyAsync(match.Id, title, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[LINEUP JOB] INotificationService.NotifyAsync failed for match {MatchId}", match.Id);
            }
        }

        private static IEnumerable<MatchLineupPlayer> MapPlayers(
            int matchId,
            string side,
            string role,
            IEnumerable<Application.DTOs.Lineup.SportsLineupPlayer> source)
        {
            return source.Select(p => new MatchLineupPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                Side = side,
                Role = role,
                ShirtNumber = p.ShirtNumber,
                PlayerName = p.Name,
                Position = p.Position,
                IsCaptain = p.IsCaptain
            });
        }
    }
}
