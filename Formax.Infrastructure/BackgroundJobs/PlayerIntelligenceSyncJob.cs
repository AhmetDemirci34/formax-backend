using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Football Intelligence v1.0 — günlük-ish (12h) player/squad ingestion. Yaklaşan maçlardaki
    /// takımların oyuncu-düzeyi zekâsını yeniler (coverage varsa). Ayrı job, ayrı concern; motor/hash
    /// değişmez. Kota koruması: cycle başına takım sınırı + provider cache (12h).
    /// </summary>
    public sealed class PlayerIntelligenceSyncJob : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan LoopDelay = TimeSpan.FromHours(12);
        private const int MaxTeamsPerCycle = 60;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PlayerIntelligenceSyncJob> _logger;

        public PlayerIntelligenceSyncJob(IServiceScopeFactory scopeFactory, ILogger<PlayerIntelligenceSyncJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[PLAYER INTEL] Job started.");
            await Task.Delay(StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Kota telemetrisi: bu turda üretilen api-football istekleri bu job'a etiketlenir.
                    using var _quotaScope = Formax.Infrastructure.Telemetry
                        .ApiFootballCallScope.Begin(nameof(PlayerIntelligenceSyncJob));

                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<PlayerIntelligenceIngestionService>();
                    await svc.IngestUpcomingAsync(MaxTeamsPerCycle, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PLAYER INTEL] Cycle failed — retry in {Delay}.", LoopDelay);
                }

                await Task.Delay(LoopDelay, stoppingToken);
            }

            _logger.LogInformation("[PLAYER INTEL] Job stopped.");
        }
    }
}
