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
    /// RESMÎ SONUÇ BOTU İŞİ — 1 dk'da bir tur (≤ 5 dk yayın → yazım hedefi). Tur yalnız zamanı gelmiş kontrol satırlarını işler (kickoff +105 dk'dan
    /// önce hiçbir maç için istek üretmez). Tek örnek döngü: turlar üst üste binmez; satır kilidi ayrıca iki işçiyi ayırır.
    /// </summary>
    public sealed class OfficialResultBotJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialResultBotJob> _log;

        public OfficialResultBotJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<OfficialResultBotJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        public static bool Enabled(IConfiguration config) => config.GetValue("OfficialSources:ResultBot:Enabled", true);

        private TimeSpan Interval => TimeSpan.FromMinutes(Math.Clamp(_config.GetValue("OfficialSources:ResultBot:IntervalMinutes", 1), 1, 15));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!Enabled(_config)) return;
            try { await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken); } catch (OperationCanceledException) { return; }
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunOnceAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "[RESULT BOT JOB] tur başarısız"); }
                try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }

        public async Task<ResultBotCycleReport> RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<OfficialResultBotService>().RunCycleAsync(DateTime.UtcNow, ct);
        }
    }

    /// <summary>
    /// RESMÎ İSTATİSTİK BOTU İŞİ — 5 dk'da bir ya da kesin sonuç yazıldığında (sinyal) uyanır; yalnız zamanı gelen satırları işler.
    /// </summary>
    public sealed class OfficialStatisticsBotJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialStatisticsBotJob> _log;

        public OfficialStatisticsBotJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<OfficialStatisticsBotJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        private TimeSpan Interval => TimeSpan.FromMinutes(Math.Clamp(_config.GetValue("OfficialSources:StatisticsBot:IntervalMinutes", 5), 1, 30));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 17.09.2026 ürün kararı: biten maçlarda yeni istatistik TOPLANMAZ (varsayılan kapalı; eski satırlar silinmez).
            if (!_config.GetValue("OfficialSources:StatisticsBot:Enabled", false)) return;
            try { await Task.Delay(TimeSpan.FromSeconds(75), stoppingToken); } catch (OperationCanceledException) { return; }
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunOnceAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "[STATS BOT JOB] tur başarısız"); }
                try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }

        public async Task<StatisticsBotCycleReport> RunOnceAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<OfficialStatisticsBotService>().RunCycleAsync(DateTime.UtcNow, ct);
        }
    }
}
