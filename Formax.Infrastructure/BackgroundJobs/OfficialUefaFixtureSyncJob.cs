using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.OfficialSources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// RESMÎ UEFA FİKSTÜR SENKRONU — üç UEFA müsabakasının yaklaşan maçlarını resmî kaynaktan alır.
    ///
    /// NEDEN AYRI JOB: api-football planı ileri takvimi vermiyor (ölçüldü 18.09.2026). Bu job API-FOOTBALL'A
    /// HİÇ ÇIKMAZ; yalnız resmî UEFA maç merkezini okur ve kanonik <c>Matches</c> satırlarını yazar. Sonuç ve
    /// skor bu job'un işi DEĞİLDİR (OfficialResultBot sahibi).
    ///
    /// Kadans: açılışta bir kontrollü tur (gecikmeli), sonra <c>OfficialSources:UefaFixtures:IntervalHours</c>
    /// (varsayılan 24 sa). Aynı süreçte ikinci tur BAŞLAMAZ (tek uçuş kilidi). Başarısız tur watermark yazmaz:
    /// son başarılı tur zamanı yalnız gerçekten başarılı okumada güncellenir.
    ///
    /// Sayfa/API açılışı bu job'u TETİKLEMEZ; kullanıcı yolunda çözülmez.
    /// </summary>
    public sealed class OfficialUefaFixtureSyncJob : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(75);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialUefaFixtureSyncJob> _log;

        /// <summary>Tek uçuş: aynı süreçte iki tur aynı anda çalışmaz.</summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        public OfficialUefaFixtureSyncJob(
            IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<OfficialUefaFixtureSyncJob> log)
        {
            _scopeFactory = scopeFactory; _config = config; _log = log;
        }

        /// <summary>Son BAŞARILI turun zamanı (teşhis; başarısız tur bunu değiştirmez).</summary>
        public DateTime? LastSuccessUtc { get; private set; }

        /// <summary>Son turun raporu (teşhis).</summary>
        public UefaFixtureSyncReport? LastReport { get; private set; }

        private bool Enabled => _config.GetValue("OfficialSources:UefaFixtures:Enabled", true);

        private TimeSpan Interval => TimeSpan.FromHours(
            Math.Clamp(_config.GetValue("OfficialSources:UefaFixtures:IntervalHours", 24), 1, 168));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!Enabled)
            {
                _log.LogInformation("[UEFA FIXTURE] Job KAPALI (OfficialSources:UefaFixtures:Enabled=false).");
                return;
            }

            _log.LogInformation("[UEFA FIXTURE] Job başladı — ilk tur {Delay} sonra, kadans {Interval}.",
                StartupDelay, Interval);

            try { await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(DateTime.UtcNow, stoppingToken).ConfigureAwait(false);
                try { await Task.Delay(Interval, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }

            _log.LogInformation("[UEFA FIXTURE] Job durdu.");
        }

        /// <summary>
        /// Tek tur (yönetici tetiği de bunu kullanır). Aynı anda ikinci tur istenirse ATLANIR.
        /// Başarısız okuma watermark yazmaz.
        /// </summary>
        public async Task<UefaFixtureSyncReport?> RunOnceAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                _log.LogInformation("[UEFA FIXTURE] Tur zaten çalışıyor — bu istek atlandı.");
                return null;
            }
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<OfficialUefaFixtureService>();
                var report = await service.ImportAsync(nowUtc, ct).ConfigureAwait(false);
                LastReport = report;

                if (report.Succeeded) LastSuccessUtc = nowUtc;
                else
                    _log.LogWarning("[UEFA FIXTURE] Tur BAŞARISIZ ({Outcome}:{Detail}) — hiçbir şey yazılmadı, " +
                        "son başarılı tur zamanı KORUNDU ({Last}).", report.Outcome, report.Detail, LastSuccessUtc);

                if (report.UnmatchedTeamReport.Count > 0)
                    _log.LogWarning("[UEFA FIXTURE] Eşleşmeyen takım(lar) — maç YAZILMADI: {Teams}",
                        string.Join(" | ", report.UnmatchedTeamReport));

                return report;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _log.LogError(ex, "[UEFA FIXTURE] Tur hata ile bitti — watermark yazılmadı.");
                return null;
            }
            finally { _gate.Release(); }
        }

        public override void Dispose()
        {
            _gate.Dispose();
            base.Dispose();
        }
    }
}
