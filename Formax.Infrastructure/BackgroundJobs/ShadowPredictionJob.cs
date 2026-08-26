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
    /// Shadow Mode V1 — yaklaşan gerçek maçlar için tahmin üretir ve yazar.
    ///
    /// KULLANICI İSTEĞİNİ BLOKE ETMEZ: kendi zamanlamasıyla çalışan bir BackgroundService'tir,
    /// istek yolunda hiçbir yeri yoktur. /detail, AI prewarm ve diğer işlere dokunmaz; yalnız
    /// okuduğu tablolar ortaktır (Matches, Teams) ve bunları AsNoTracking ile okur.
    ///
    /// Bounded concurrency: aynı anda tek cycle çalışır (döngü sıralıdır). Failure isolation:
    /// bir maçın hatası cycle'ı, bir cycle'ın hatası job'ı düşürmez.
    /// </summary>
    public sealed class ShadowPredictionJob : BackgroundService
    {

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ShadowPredictionJob> _logger;
        private readonly PredictionEngineOptions _options;
        private readonly ShadowHealthState _health;

        public ShadowPredictionJob(IServiceScopeFactory scopeFactory, ILogger<ShadowPredictionJob> logger,
            PredictionEngineOptions options, ShadowHealthState health)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options;
            _health = health;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.IsUsable())
            {
                _logger.LogWarning("[SHADOW] engine files not found under {Root} - job disabled.", _options.EngineRoot);
                _health.ShadowDisabled("engine files not found under " + _options.EngineRoot);
                return;
            }

            var startupDelay = TimeSpan.FromSeconds(_options.StartupDelaySeconds);
            var loopDelay = TimeSpan.FromHours(_options.LoopHours);
            _health.ShadowStarted();
            _logger.LogInformation("[SHADOW] Job started. Horizon {Days}d, startup delay {Startup}, loop {Loop}.",
                _options.HorizonDays, startupDelay, loopDelay);
            // Baslangic gecikmesi de iptal edilebilir; host acilir acilmaz kapatilirsa bu bir hata degildir.
            try { await Task.Delay(startupDelay, stoppingToken); }
            catch (OperationCanceledException) { _logger.LogInformation("[SHADOW] Job stopped gracefully before its first cycle."); return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _health.ShadowCycleStarted();
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<ShadowPredictionService>();
                    var report = await svc.RunCycleAsync(_options.HorizonDays, stoppingToken);
                    _health.ShadowCycleSucceeded(report);

                    _logger.LogInformation(
                        "[SHADOW] cycle done in {Ms} ms — seen {Seen}, predicted {Pred} (accepted {Acc}/rejected {Rej}), " +
                        "inserted {Ins}, already published {Dup}, conflicted {Conf}, failed {Fail}, evidence through {Through:yyyy-MM-dd}",
                        report.ElapsedMs, report.UpcomingMatchesSeen, report.Predicted, report.Accepted,
                        report.Rejected, report.Inserted, report.AlreadyPublished, report.NotInsertedDueToConflict, report.Failed,
                        report.RatingEvidenceThrough);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _health.ShadowCycleFailed(ex);
                    _logger.LogError(ex, "[SHADOW] cycle failed — retry in {Delay}.", loopDelay);
                }

                // Bekleme de iptal edilebilir olmalı ve iptal bir HATA değildir: kapanış sinyali
                // döngünün %99'unda tam buraya, Task.Delay'in içine düşer. Bu catch olmadan
                // OperationCanceledException ExecuteAsync'ten dışarı sızar, aşağıdaki kapanış
                // satırına hiç ulaşılmaz ve .NET 8'in varsayılan
                // BackgroundServiceExceptionBehavior.StopHost davranışıyla host'u düşürebilir.
                try
                {
                    await Task.Delay(loopDelay, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
            }

            _logger.LogInformation("[SHADOW] Job stopped gracefully.");
        }
    }

    /// <summary>Biten maçların sonuçlarını tahminlere iliştirir. Tahmin satırına dokunmaz.</summary>
    public sealed class PredictionSettlementJob : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan LoopDelay = TimeSpan.FromHours(3);
        private const int MaxRowsPerCycle = 500;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PredictionSettlementJob> _logger;
        private readonly ShadowHealthState _health;

        public PredictionSettlementJob(IServiceScopeFactory scopeFactory, ILogger<PredictionSettlementJob> logger,
            ShadowHealthState health)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _health = health;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _health.SettlementStarted();
            _logger.LogInformation("[SETTLEMENT] Job started.");
            try { await Task.Delay(StartupDelay, stoppingToken); }
            catch (OperationCanceledException) { _logger.LogInformation("[SETTLEMENT] Job stopped gracefully before its first cycle."); return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<PredictionSettlementService>();
                    var report = await svc.SettleAsync(MaxRowsPerCycle, stoppingToken);
                    _health.SettlementCycleSucceeded(report.Settled);

                    if (report.Candidates > 0)
                        _logger.LogInformation(
                            "[SETTLEMENT] {Ms} ms — candidates {C}, settled {S}, no result yet {N}, " +
                            "already settled {A}, rejected as early {E}, failed {F}",
                            report.ElapsedMs, report.Candidates, report.Settled, report.NoResultYet,
                            report.AlreadySettled, report.RejectedAsEarly, report.Failed);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _health.SettlementCycleFailed(ex);
                    _logger.LogError(ex, "[SETTLEMENT] cycle failed — retry in {Delay}.", LoopDelay);
                }

                // Aynı gerekçe: iptal beklemenin içine düşer ve bir hata değildir.
                try
                {
                    await Task.Delay(LoopDelay, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
            }

            _logger.LogInformation("[SETTLEMENT] Job stopped gracefully.");
        }
    }
}
