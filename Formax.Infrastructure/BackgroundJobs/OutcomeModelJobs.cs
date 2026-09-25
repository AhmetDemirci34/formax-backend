using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// OLASI SONUÇ MODEL İŞİ — günde bir zamansal geriye dönük test + kalibrasyon koşusu (son koşu 20 saatten eskiyse), ardından
    /// 30 dk'da bir snapshot turu. Kullanıcı isteği bu işi tetiklemez; dış istek yoktur (yalnız DB).
    /// </summary>
    public sealed class OutcomeModelJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<OutcomeModelJob> _log;

        public OutcomeModelJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<OutcomeModelJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        private TimeSpan SnapshotInterval => TimeSpan.FromMinutes(Math.Clamp(_config.GetValue("Outcomes:SnapshotIntervalMinutes", 30), 5, 240));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("Outcomes:Enabled", true)) return;
            try { await Task.Delay(TimeSpan.FromSeconds(_config.GetValue("Outcomes:StartupDelaySeconds", 90)), stoppingToken); }
            catch (OperationCanceledException) { return; }
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Yayın politikası, günlük koşu yeni ham matris üretmeden ÖNCE başlatılır: bootstrap şu an yayında olan
                    // snapshot'ların koşusunu sabitler, yeni koşunun tek tarihli matrisi yayına sızamaz.
                    await EnsurePublicationBootstrappedAsync(stoppingToken);
                    await EnsureTrainedAsync(stoppingToken);
                    await RunSnapshotsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "[OUTCOME JOB] tur başarısız"); }
                try { await Task.Delay(SnapshotInterval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }

        public async Task EnsurePublicationBootstrappedAsync(CancellationToken ct)
        {
            if (!_config.GetValue("EligibilityPublication:Enabled", true)) return;
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<EligibilityPublicationService>().EnsureBootstrappedAsync(DateTime.UtcNow, ct);
        }

        public async Task EnsureTrainedAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FormaxDbContext>();
            var last = await db.PredictionModelRuns.AsNoTracking()
                .Where(r => r.ModelVersion == Application.Services.Outcomes.OutcomeModelVersion.Current)
                .OrderByDescending(r => r.CompletedAtUtc).Select(r => (DateTime?)r.CompletedAtUtc).FirstOrDefaultAsync(ct);
            if (last != null && DateTime.UtcNow - last.Value < TimeSpan.FromHours(20)) return;
            await scope.ServiceProvider.GetRequiredService<OutcomeModelTrainingService>().RunAsync(DateTime.UtcNow, ct);
        }

        public async Task<SnapshotCycleReport> RunSnapshotsAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<MatchPredictionSnapshotService>().RunAsync(DateTime.UtcNow, ct);
        }
    }
}

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// TAHMİN YENİLEME KUYRUĞU + CANLI KARNE İŞİ — dakikada bir: zamanı gelmiş (debounce geçmiş) doğrulanmış olay isteklerini işler,
    /// başlama anında snapshot'ı karneye kilitler, bitmiş maçı değerlendirir. Kuyruk DB'dedir: restart'ta kaybolmaz. Dış istek yok.
    /// </summary>
    public sealed class PredictionRecomputeJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<PredictionRecomputeJob> _log;

        public PredictionRecomputeJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<PredictionRecomputeJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("Outcomes:Enabled", true)) return;
            try { await Task.Delay(TimeSpan.FromSeconds(_config.GetValue("Outcomes:QueueStartupDelaySeconds", 60)), stoppingToken); }
            catch (OperationCanceledException) { return; }
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunOnceAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "[PREDICTION QUEUE JOB] tur başarısız"); }
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_config.GetValue("Outcomes:QueueIntervalSeconds", 60), 15, 600)), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        public async Task<RecomputeCycleReport> RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<PredictionRecomputeWorker>().RunOnceAsync(DateTime.UtcNow, ct);
        }
    }
}

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// HAFTALIK UYGUNLUK YAYINI — Pazartesi 05:00 Europe/Istanbul (hafta sonu sonuç yazımı bitmiş olur). Kesim tarihi planlı anın
    /// kendisidir (deterministik): süreç o saatte kapalıysa açıldığında KAÇIRILAN SON planlı an aynı kesimle çalışır. Aynı hafta ikinci
    /// yayın yazılmaz (tekil RunKey). Yalnız bootstrap'tan SONRAKİ planlı anlar işlenir. Kullanıcı isteği bu işi tetiklemez; dış
    /// istek yok (yalnız DB + mevcut backtest).
    /// </summary>
    public sealed class EligibilityPublicationJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<EligibilityPublicationJob> _log;

        public EligibilityPublicationJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<EligibilityPublicationJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("EligibilityPublication:Enabled", true)) return;
            try { await Task.Delay(TimeSpan.FromSeconds(_config.GetValue("EligibilityPublication:StartupDelaySeconds", 5)), stoppingToken); }
            catch (OperationCanceledException) { return; }
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunDueAsync(DateTime.UtcNow, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "[ELIGIBILITY PUBLICATION JOB] tur başarısız"); }
                var now = DateTime.UtcNow;
                var wait = Application.Services.Outcomes.EligibilityEvaluationSchedule.NextSlotAfter(now) - now + TimeSpan.FromSeconds(30);
                if (wait > TimeSpan.FromHours(6)) wait = TimeSpan.FromHours(6);
                if (wait < TimeSpan.FromMinutes(1)) wait = TimeSpan.FromMinutes(1);
                try { await Task.Delay(wait, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }

        /// <summary>Planlı an geldiyse ve henüz yayımlanmadıysa yayın turunu çalıştırır; aksi hâlde hiçbir şey yapmaz.</summary>
        public async Task<string> RunDueAsync(DateTime nowUtc, CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<EligibilityPublicationService>();
            var db = scope.ServiceProvider.GetRequiredService<FormaxDbContext>();
            await svc.EnsureBootstrappedAsync(nowUtc, ct);
            var bootstrapAt = await db.MarketEligibilityPublicationRuns.AsNoTracking()
                .Where(r => r.RunKey == EligibilityPublicationService.BootstrapRunKey).Select(r => (DateTime?)r.StartedAtUtc).FirstOrDefaultAsync(ct);
            var slot = Application.Services.Outcomes.EligibilityEvaluationSchedule.LatestSlotAtOrBefore(nowUtc);
            if (bootstrapAt == null || slot <= bootstrapAt.Value) return "NOT_DUE";
            if (!await EligibilityPublicationService.Gate.WaitAsync(TimeSpan.Zero, ct)) return "BUSY";
            try
            {
                var report = await svc.RunAsync(new[] { slot }, EligibilityPublicationMode.Publish, EligibilityPublicationSources.Scheduled, nowUtc, ct);
                var result = report.Cutoffs.FirstOrDefault()?.Result ?? "NONE";
                if (result == "PUBLISHED")
                    _log.LogInformation("[ELIGIBILITY PUBLICATION JOB] haftalık yayın: kesim={Slot:O} süre={Ms}ms geçiş={Changes}", slot, report.TotalMs,
                        report.Cutoffs.Sum(c => c.Transitions.Count(t => t.Changed)));
                return result;
            }
            finally { EligibilityPublicationService.Gate.Release(); }
        }
    }
}
