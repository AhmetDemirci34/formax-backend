using System.Net.Http;
using Formax.Infrastructure.Normalize.Mapping;
using Formax.Infrastructure.Normalize.Raw;
using Formax.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Formax.Infrastructure.Providers.Sources.OpenLigaDb;

/// <summary>
/// OpenLigaDB provider DI kaydı (FAZ 13 onaylı ilk Core provider).
/// Sağlayıcıyı <see cref="ProviderEngineServiceCollectionExtensions.AddProvider{TProvider}"/> ile kaydeder;
/// Registry/Orchestrator otomatik keşfeder. Provider hiçbir ayar tutmaz — tüm ayarlar merkezi
/// yapılandırmadan okunur ve başlangıç kayıtları <c>ProviderBootstrap</c> katmanında yapılır.
/// </summary>
public static class OpenLigaDbServiceCollectionExtensions
{
    public static IServiceCollection AddOpenLigaDbProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<HttpClient>();
        services.AddProvider<OpenLigaDbProvider>();

        // OpenLigaDB ham JSON → ortak RawFixture mapper'ı (yalnızca bu provider'a özgü).
        services.AddProviderMapper<RawFixture, OpenLigaDbFixtureMapper>();

        return services;
    }
}
