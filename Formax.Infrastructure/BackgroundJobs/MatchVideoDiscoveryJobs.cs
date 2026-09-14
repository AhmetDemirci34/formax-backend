using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// KALICI VİDEO KEŞİF KUYRUĞU İŞİ — 5 dk'da bir; tur başına sınırlı maç. Kullanıcı sayfası açmadan çalışır.
    /// Açılışta 3 dk bekler: süreç açılışında çok sayıda iş aynı anda başlayıp thread pool'u tüketmesin.
    /// </summary>
    public sealed class MatchVideoDiscoveryJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<MatchVideoDiscoveryJob> _log;

        public MatchVideoDiscoveryJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<MatchVideoDiscoveryJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(_config.GetValue("PostMatch:Video:StartupDelayMinutes", 3)), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_config.GetValue("PostMatch:Video:Enabled", true))
                {
                    try
                    {
                        using var scope = _scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<PostMatch.MatchVideoDiscoveryQueueService>()
                            .RunCycleAsync(DateTime.UtcNow, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception ex) { _log.LogError(ex, "[VIDEO-QUEUE] tur başarısız"); }
                }
                try { await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _config.GetValue("PostMatch:Video:QueueIntervalMinutes", 5))), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>RESMÎ KAYNAK KATALOĞU İŞİ — 12 saatte bir Wikidata + resmî site kanıtıyla kaynak keşfi.</summary>
    public sealed class OfficialVideoSourceCatalogJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialVideoSourceCatalogJob> _log;

        public OfficialVideoSourceCatalogJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<OfficialVideoSourceCatalogJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(_config.GetValue("PostMatch:Video:Catalog:StartupDelayMinutes", 4)), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_config.GetValue("PostMatch:Video:Catalog:Enabled", true))
                {
                    try
                    {
                        using var scope = _scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<PostMatch.OfficialVideoSourceDiscoveryService>()
                            .RunAsync(DateTime.UtcNow, _config.GetValue("PostMatch:Video:Catalog:MaxTeamsPerRun", 60), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception ex) { _log.LogError(ex, "[VIDEO-SOURCES] katalog keşfi başarısız"); }
                }
                try { await Task.Delay(TimeSpan.FromHours(Math.Max(1, _config.GetValue("PostMatch:Video:Catalog:IntervalHours", 12))), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
