using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// SAATLİK PUAN DURUMU PROJEKSİYONU.
    ///
    /// Kapsamdaki her lig için mevcut sezonun tamamlanmış maçlarından puan tablosu üretir
    /// ve <c>LeagueStandingsSnapshots</c>'a yazar. Kullanıcı tıklamasından TAMAMEN bağımsızdır:
    /// kaç kişi maç detayı açarsa açsın hesap saatte bir yapılır.
    ///
    /// DIŞ İSTEK YOK: ne api-football ne başka sağlayıcı çağrılır, hiçbir sayfa kazınmaz.
    /// Girdi yalnız kendi Matches tablomuzdur → kota etkisi sıfır.
    ///
    /// EŞZAMANLILIK: tur içi statik kilit ile aynı job iki kez paralel çalışmaz (uzun süren
    /// bir tur, bir sonraki tetiklemeyle üst üste binmez).
    /// </summary>
    public sealed class HourlyStandingsProjectionJob : BackgroundService
    {
        private static readonly SemaphoreSlim RunLock = new(1, 1);

        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<HourlyStandingsProjectionJob> _log;

        public HourlyStandingsProjectionJob(IServiceScopeFactory scopeFactory, ILogger<HourlyStandingsProjectionJob> log)
        {
            _scopeFactory = scopeFactory;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);

                try { await Task.Delay(Interval, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        /// <summary>Tek tur — admin ucundan elle de tetiklenebilir.</summary>
        public async Task<int> RunOnceAsync(CancellationToken ct)
        {
            // Aynı anda ikinci bir tur BAŞLAMAZ (çakışan yazma / çift hesap yok).
            if (!await RunLock.WaitAsync(0, ct).ConfigureAwait(false))
            {
                _log.LogInformation("Standings projection already running — skipped.");
                return 0;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ILeagueStandingsService>();

                var started = DateTime.UtcNow;
                var refreshed = await service.RefreshCurrentSeasonAsync(ct).ConfigureAwait(false);

                _log.LogInformation(
                    "Standings projection completed — {Leagues} league(s) in {Ms} ms.",
                    refreshed, (int)(DateTime.UtcNow - started).TotalMilliseconds);

                return refreshed;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Standings projection failed.");
                return 0;
            }
            finally
            {
                RunLock.Release();
            }
        }
    }
}
