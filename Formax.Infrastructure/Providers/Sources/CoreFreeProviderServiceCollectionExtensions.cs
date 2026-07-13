using System.Net.Http;
using Formax.Infrastructure.Normalize.Mapping;
using Formax.Infrastructure.Normalize.Raw;
using Formax.Infrastructure.Providers.Sources.FootballDataUk;
using Formax.Infrastructure.Providers.Sources.Metadata;
using Formax.Infrastructure.Providers.Sources.News;
using Formax.Infrastructure.Providers.Sources.StatsBomb;
using Formax.Infrastructure.Providers.Sources.Weather;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Formax.Infrastructure.Providers.Sources;

/// <summary>
/// Core Free Provider Pack DI kaydı — Core Strategy'de onaylanan ÜCRETSİZ/AÇIK sağlayıcılar.
/// Mevcut Provider Platform kullanılır (AddProvider / AddProviderMapper); yeni altyapı yoktur.
/// Ayarlar/zamanlamalar mevcut <c>ProviderBootstrap</c> katmanında seed edilir.
/// </summary>
public static class CoreFreeProviderServiceCollectionExtensions
{
    public static IServiceCollection AddCoreFreeProviders(this IServiceCollection services)
    {
        services.TryAddSingleton<HttpClient>();

        // Fixture (uçtan uca → Normalize → … → Domain Match) + RawFixture mapper'ları
        services.AddProvider<FootballDataUkProvider>();
        services.AddProviderMapper<RawFixture, FootballDataUkFixtureMapper>();
        services.AddProvider<StatsBombProvider>();
        services.AddProviderMapper<RawFixture, StatsBombFixtureMapper>();

        // Weather (uçtan uca → Normalize → Merge → Conflict → MatchWeather) + RawWeather mapper'ları
        services.AddProvider<OpenMeteoProvider>();
        services.AddProviderMapper<RawWeather, OpenMeteoWeatherMapper>();
        services.AddProvider<MetNorwayProvider>();

        // News
        services.AddProvider<RssNewsProvider>();
        services.AddProvider<GdeltProvider>();

        // Metadata
        services.AddProvider<WikidataProvider>();
        services.AddProvider<WikipediaProvider>();
        services.AddProvider<OpenStreetMapProvider>();

        return services;
    }
}
