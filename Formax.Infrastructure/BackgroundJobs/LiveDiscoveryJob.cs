using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Live.Discovery;
using Formax.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// FORMAX Live Data Engine — Global Live Discovery ingestion job.
    ///
    /// Canlı pencere maçlarını (kickoff [-10dk, +150dk], kapanmamış statü) tarar,
    /// açık kaynaklardan (GlobalLiveDiscoveryService) canlı skoru keşfeder ve
    /// mevcut <see cref="MatchLiveStats"/> + <see cref="MatchLiveEvent"/> tablolarına yazar.
    /// LiveIntelligenceEngine bu tabloları okur → motor HİÇ değişmeden gerçek canlı veriyle
    /// beslenir. Tek üçüncü-taraf API'ye bağımlı değildir; düşük güvende yazmaz (uydurma yok).
    /// </summary>
    public sealed class LiveDiscoveryJob : BackgroundService
    {
        private static readonly TimeSpan LoopDelay = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan WindowBefore = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan WindowAfter = TimeSpan.FromMinutes(150);
        private const int MinConfidence = 50;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LiveDiscoveryJob> _logger;

        public LiveDiscoveryJob(IServiceScopeFactory scopeFactory, ILogger<LiveDiscoveryJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[LIVE DISC] Global Live Discovery job started");
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

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
                    _logger.LogError(ex, "[LIVE DISC] cycle failed — will retry in {Delay}", LoopDelay);
                }

                await Task.Delay(LoopDelay, stoppingToken);
            }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var matchRepo = sp.GetRequiredService<IMatchReadRepository>();
            var teamRepo = sp.GetRequiredService<ITeamReadRepository>();
            var discovery = sp.GetRequiredService<GlobalLiveDiscoveryService>();
            var statsRepo = sp.GetRequiredService<IMatchLiveStatsRepository>();
            var eventRepo = sp.GetRequiredService<IMatchLiveEventIngestionRepository>();

            var now = DateTime.UtcNow;
            var windowMatches = matchRepo.GetUpcomingMatches(now - WindowAfter, now + WindowBefore);
            var live = windowMatches.Where(m => !IsClosed(m.Status)).ToList();

            if (live.Count == 0)
            {
                _logger.LogDebug("[LIVE DISC] no matches in live window at {Time}", now);
                return;
            }

            _logger.LogInformation("[LIVE DISC] scanning {Count} in-window match(es) at {Time}", live.Count, now);

            foreach (var match in live)
            {
                ct.ThrowIfCancellationRequested();

                var home = teamRepo.GetById(match.HomeTeamId)?.Name ?? match.HomeTeam?.Name ?? string.Empty;
                var away = teamRepo.GetById(match.AwayTeamId)?.Name ?? match.AwayTeam?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away))
                    continue;

                DiscoveredLiveSignal signal;
                try
                {
                    signal = await discovery.DiscoverForMatchAsync(
                        match.Id, home, away, match.League ?? string.Empty, string.Empty, match.MatchDate, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[LIVE DISC] discovery failed for match {MatchId}", match.Id);
                    continue;
                }

                if (!signal.HasScore || signal.Confidence < MinConfidence)
                    continue;

                await PersistAsync(match, home, away, signal, statsRepo, eventRepo, now, ct);
            }
        }

        private async Task PersistAsync(
            Match match, string home, string away, DiscoveredLiveSignal signal,
            IMatchLiveStatsRepository statsRepo, IMatchLiveEventIngestionRepository eventRepo,
            DateTime now, CancellationToken ct)
        {
            var existing = statsRepo.GetByMatchId(match.Id);
            var prevHome = existing?.HomeScore ?? 0;
            var prevAway = existing?.AwayScore ?? 0;
            var minute = signal.Minute ?? existing?.Minute ?? 0;

            // Gol olayı YALNIZ gerçek skor artışından türetilir (uydurma değil).
            var events = new List<MatchLiveEvent>();
            for (var i = prevHome; i < signal.HomeScore; i++)
                events.Add(GoalEvent(match.Id, minute, home));
            for (var i = prevAway; i < signal.AwayScore; i++)
                events.Add(GoalEvent(match.Id, minute, away));

            var stats = new MatchLiveStats
            {
                MatchId = match.Id,
                HomeScore = signal.HomeScore,
                AwayScore = signal.AwayScore,
                Minute = signal.Minute ?? existing?.Minute,
                Phase = existing?.Phase ?? string.Empty,
                UpdatedAt = now
            };

            await statsRepo.UpsertAsync(stats, ct);
            await statsRepo.SaveChangesAsync(ct);

            if (events.Count > 0)
            {
                await eventRepo.AddNewEventsAsync(match.Id, events, ct);
                await eventRepo.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "[LIVE DISC] match {MatchId} | {Home} {H}-{A} {Away} | min={Min} conf={Conf} src={Src} newGoals={Goals}",
                match.Id, home, signal.HomeScore, signal.AwayScore, away, signal.Minute, signal.Confidence,
                signal.SourceCount, events.Count);
        }

        private static MatchLiveEvent GoalEvent(int matchId, int minute, string team) => new()
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            EventType = "Goal",
            Minute = minute,
            Team = team,
            Player = null,
            Detail = "Açık kaynak skor doğrulaması",
            ImpactScore = 90,
            CreatedAt = DateTime.UtcNow
        };

        private static bool IsClosed(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            var s = status.Trim().ToLowerInvariant();
            return s is "finished" or "ended" or "ft" or "cancelled" or "canceled" or "postponed" or "abandoned";
        }
    }
}
