using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Radar.Intelligence.Commentary;
using Formax.Application.Services.Radar.Intelligence.Match;
using Formax.Application.Services.Radar.Intelligence.News;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// MVP Freeze — Cold Start düzeltmesi.
    ///
    /// News Intelligence → Match Intelligence → Commentary snapshot üretimi ESKİDEN
    /// Program.cs içinde, app.Run() ÖNCESİNDE senkron çalışıyordu. 120 günlük pencerede
    /// ~13.6k maç işlendiği için API 5 dakikadan uzun süre hiç istek kabul etmiyordu.
    ///
    /// Aynı üç çağrı, aynı sırayla ve aynı parametrelerle buraya taşındı. Tek fark:
    /// artık app.Run() SONRASINDA arka planda çalışır. Üretilen veri ve motorların
    /// kendisi DEĞİŞMEDİ — yalnız çalışma anı değişti.
    ///
    /// Sıra önemlidir: Match Intelligence enricher'ı News Intelligence çıktısını okur,
    /// Commentary de Match Intelligence çıktısını okur.
    /// </summary>
    public sealed class RadarIntelligenceBuildJob : BackgroundService
    {
        // Program.cs'teki eski değerle birebir aynı pencere.
        private const int LookbackDays = 120;

        // API'nin trafiğe açılmasını engellememek için ilk tur gecikmeli başlar.
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan LoopDelay = TimeSpan.FromHours(6);

        private readonly ILogger<RadarIntelligenceBuildJob> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public RadarIntelligenceBuildJob(
            ILogger<RadarIntelligenceBuildJob> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[RADAR INTEL JOB] started (lookback={Days}d)", LookbackDays);

            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RADAR INTEL JOB] build cycle failed (non-fatal).");
                }

                try
                {
                    await Task.Delay(LoopDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var fromUtc = DateTime.UtcNow.AddDays(-LookbackDays);

            // 1) News Intelligence — match enrichment bunu okuduğu için ÖNCE çalışır.
            var newsIntel = scope.ServiceProvider.GetRequiredService<INewsIntelligenceService>();
            var newsCount = await newsIntel.BuildAsync(fromUtc, ct);
            _logger.LogInformation("[NEWS INTEL] built {Count} news match snapshot(s).", newsCount);

            // 2) Match Intelligence snapshot'ları (idempotent upsert).
            var matchIntel = scope.ServiceProvider.GetRequiredService<IMatchIntelligenceService>();
            var intelCount = await matchIntel.BuildUpcomingAsync(fromUtc, ct);
            _logger.LogInformation("[MATCH INTEL] built {Count} match intelligence snapshot(s).", intelCount);

            // 3) Commentary — intelligence + news üzerinden deterministik metin.
            var commentary = scope.ServiceProvider.GetRequiredService<ICommentaryService>();
            var commentaryCount = await commentary.BuildAsync(fromUtc, ct);
            _logger.LogInformation("[COMMENTARY] built {Count} commentary snapshot(s).", commentaryCount);
        }
    }
}
