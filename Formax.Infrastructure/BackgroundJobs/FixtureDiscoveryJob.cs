using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// FORMAX Data Engine v1 — Global Fixture Discovery scheduler.
    ///
    /// • Başlangıçta + her 6 saatte bir: önümüzdeki 30 günü TÜM açık provider'lardan
    ///   yeniden keşfeder ve Fixtures tablosuna upsert eder (yeni maç / saat değişimi /
    ///   iptal → güncellenir).
    /// • Sürekli çalışmaz; düşük maliyetli periyodik tarama.
    ///
    /// Mevcut hiçbir sistemi (News/Discovery/Reasoning/Scenario) etkilemez; yalnız
    /// veri omurgasını (Fixtures) besler.
    /// </summary>
    public sealed class FixtureDiscoveryJob : BackgroundService
    {
        private static readonly TimeSpan RefreshEvery = TimeSpan.FromHours(6);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
        private const int HorizonDays = 30;
        private const int MinConfidence = 60;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FixtureDiscoveryJob> _logger;

        public FixtureDiscoveryJob(IServiceScopeFactory scopeFactory, ILogger<FixtureDiscoveryJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[FIXTURE] Discovery scheduler started (horizon {Days}g, her {H}s).",
                HorizonDays, RefreshEvery.TotalHours);

            try { await Task.Delay(StartupDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FIXTURE] Discovery döngüsü başarısız — {H}s sonra tekrar.", RefreshEvery.TotalHours);
                }

                try { await Task.Delay(RefreshEvery, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var discovery = scope.ServiceProvider.GetRequiredService<FixtureDiscoveryService>();
            var repo = scope.ServiceProvider.GetRequiredService<IFixtureRepository>();

            var from = DateOnly.FromDateTime(DateTime.UtcNow);
            var to = from.AddDays(HorizonDays);

            var results = await discovery.DiscoverAsync(from, to, MinConfidence, ct);
            var (added, updated) = await repo.UpsertAsync(results, ct);

            _logger.LogInformation(
                "[FIXTURE] Keşif tamam — {Total} maç (≥{Min} güven), {Added} yeni, {Updated} güncellendi.",
                results.Count, MinConfidence, added, updated);
        }
    }
}
