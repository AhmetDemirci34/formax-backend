using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Predictions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// SHADOW B — "aynı model + maç öncesi kanıt düzeltmesi" deney hattının zamanlayıcısı.
    ///
    /// SHADOW A'DAN TAMAMEN AYRIDIR: ayrı job, ayrı servis, ayrı tablolar, ayrı config anahtarı.
    /// Kapatıldığında Shadow A'nın davranışı zerre kadar değişmez; açıldığında da A'nın ürettiği
    /// hiçbir satıra dokunmaz — yalnız okur.
    ///
    /// A'nın peşinden koşar: taban tahmin A tarafından yazılmış olmalıdır, bu yüzden başlangıç
    /// gecikmesi A'nınkinden uzundur.
    /// </summary>
    public sealed class ShadowBPredictionJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ShadowBPredictionJob> _logger;
        private readonly ShadowBOptions _options;
        private readonly ShadowHealthState _health;

        public ShadowBPredictionJob(IServiceScopeFactory scopeFactory, ILogger<ShadowBPredictionJob> logger,
            ShadowBOptions options, ShadowHealthState health)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options;
            _health = health;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var startupDelay = TimeSpan.FromSeconds(_options.StartupDelaySeconds);
            var loopDelay = TimeSpan.FromHours(_options.LoopHours);
            _health.ShadowBStarted();
            _logger.LogInformation(
                "[SHADOW-B] Job started ({Version}). Horizon {Days}d, startup delay {Startup}, loop {Loop}.",
                NewsAdjustOptions.Version, _options.HorizonDays, startupDelay, loopDelay);

            try { await Task.Delay(startupDelay, stoppingToken); }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[SHADOW-B] Job stopped gracefully before its first cycle.");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _health.ShadowBCycleStarted();
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<ShadowBPredictionService>();
                    var report = await svc.RunCycleAsync(_options.HorizonDays, stoppingToken);
                    _health.ShadowBCycleSucceeded(report);

                    _logger.LogInformation(
                        "[SHADOW-B] cycle done in {Ms} ms — base rows {Base}, considered {Con}, " +
                        "inserted {Ins}, already published {Dup}, adjusted {Adj}, no evidence {NoEv}, " +
                        "identity unresolved {Unres}, conflicted {Conf}, failed {Fail}, settled {Set}",
                        report.ElapsedMs, report.BaseRowsSeen, report.Considered, report.Inserted,
                        report.AlreadyPublished, report.Adjusted, report.NoEvidence,
                        report.IdentityUnresolved, report.NotInsertedDueToConflict, report.Failed, report.Settled);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _health.ShadowBCycleFailed(ex);
                    _logger.LogError(ex, "[SHADOW-B] cycle failed — retry in {Delay}.", loopDelay);
                }

                // İptal beklemenin içine düşer ve bir HATA değildir (Shadow A ile aynı gerekçe).
                try { await Task.Delay(loopDelay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }

            _logger.LogInformation("[SHADOW-B] Job stopped gracefully.");
        }
    }

    /// <summary>Shadow B zamanlama ayarları. Hiçbiri MODEL parametresi değildir.</summary>
    public sealed class ShadowBOptions
    {
        public int StartupDelaySeconds { get; set; } = 180;
        public double LoopHours { get; set; } = 3;
        public int HorizonDays { get; set; } = 8;
    }
}
