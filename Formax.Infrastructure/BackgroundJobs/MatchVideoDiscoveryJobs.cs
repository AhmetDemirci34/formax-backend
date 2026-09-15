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
                try { await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _config.GetValue("PostMatch:Video:QueueIntervalMinutes", 3))), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>RESMÎ KAYNAK KATALOĞU İŞİ — 3 saatte bir lig sitesi bağlantısı + Wikidata EntityData + site sameAs kanıtıyla kaynak ve akış keşfi.</summary>
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
                            .RunAsync(DateTime.UtcNow, _config.GetValue("PostMatch:Video:Catalog:MaxTeamsPerRun", 80), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception ex) { _log.LogError(ex, "[VIDEO-SOURCES] katalog keşfi başarısız"); }
                }
                try { await Task.Delay(TimeSpan.FromHours(Math.Max(1, _config.GetValue("PostMatch:Video:Catalog:IntervalHours", 3))), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// RESMÎ SİTE AKIŞ TARAMA İŞİ — 20 dk'da bir zamanı gelen video sitemap / RSS / Atom / ana sayfa JSON-LD akışlarını okur
    /// ve girişleri kalıcı yazar. Kullanıcı sayfası açmadan çalışır; robots.txt ve host kuralları nezaket katmanındadır.
    /// </summary>
    public sealed class OfficialWebFeedCrawlJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialWebFeedCrawlJob> _log;

        public OfficialWebFeedCrawlJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<OfficialWebFeedCrawlJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(_config.GetValue("PostMatch:Video:Crawl:StartupDelayMinutes", 5)), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_config.GetValue("PostMatch:Video:Crawl:Enabled", true))
                {
                    try
                    {
                        using var scope = _scopes.CreateScope();
                        var (feeds, fresh) = await scope.ServiceProvider.GetRequiredService<PostMatch.OfficialWebFeedCrawler>()
                            .CrawlDueAsync(DateTime.UtcNow, Math.Clamp(_config.GetValue("PostMatch:Video:Crawl:MaxFeedsPerRun", 30), 1, 200), stoppingToken);
                        if (feeds > 0) _log.LogInformation("[VIDEO-CRAWL] okunan akış={Feeds} yeni giriş={Fresh}", feeds, fresh);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception ex) { _log.LogError(ex, "[VIDEO-CRAWL] tur başarısız"); }
                }
                try { await Task.Delay(TimeSpan.FromMinutes(Math.Max(5, _config.GetValue("PostMatch:Video:Crawl:IntervalMinutes", 20))), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>
    /// KALICI VİDEO YENİDEN DOĞRULAMA İŞİ — 30 dk'da bir, 7 günden eski doğrulamalı kabul edilmiş kayıtları güncel kurallarla
    /// yeniden sınar; geçemeyeni kapatır ve maçı yeniden kuyruğa alır.
    /// </summary>
    public sealed class MatchVideoRevalidationJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<MatchVideoRevalidationJob> _log;

        public MatchVideoRevalidationJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<MatchVideoRevalidationJob> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(_config.GetValue("PostMatch:Video:Revalidation:StartupDelayMinutes", 2)), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_config.GetValue("PostMatch:Video:Revalidation:Enabled", true))
                {
                    try
                    {
                        using var scope = _scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<PostMatch.MatchVideoRevalidationService>()
                            .RunAsync(DateTime.UtcNow, _config.GetValue("PostMatch:Video:Revalidation:MaxPerRun", 100), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception ex) { _log.LogError(ex, "[VIDEO-REVALIDATE] tur başarısız"); }
                }
                try { await Task.Delay(TimeSpan.FromMinutes(Math.Max(5, _config.GetValue("PostMatch:Video:Revalidation:IntervalMinutes", 30))), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
