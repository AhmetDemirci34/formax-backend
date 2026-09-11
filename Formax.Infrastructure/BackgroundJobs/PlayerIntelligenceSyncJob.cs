using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
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

        /// <summary>
        /// Tur başına takım tavanı. 60 iken bu job günde ~120 takım × ~4 istek = kotanın tamamını
        /// tek başına yiyordu. Oyuncu zekâsı ZENGİNLEŞTİRMEdir: fikstür/sonuç zincirinin bütçesini
        /// tüketmemeli. Artık YAVAŞ DÖNEN bir tazeleme: watermark (FreshHours/EmptyBackoffHours)
        /// semantiği aynen korunur, yalnız tur başına işlenen takım sayısı config'ten sınırlanır.
        /// </summary>
        private const int DefaultMaxTeamsPerCycle = 8;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<PlayerIntelligenceSyncJob> _logger;

        public PlayerIntelligenceSyncJob(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<PlayerIntelligenceSyncJob> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
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
                    var maxTeams = Math.Clamp(
                        _config.GetValue("PlayerIntelligence:MaxTeamsPerCycle", DefaultMaxTeamsPerCycle), 1, 60);
                    await svc.IngestUpcomingAsync(maxTeams, stoppingToken);
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
