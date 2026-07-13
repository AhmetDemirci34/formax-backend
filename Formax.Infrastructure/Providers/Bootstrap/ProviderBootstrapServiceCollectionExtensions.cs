using Formax.Infrastructure.Providers.Sources;
using Formax.Infrastructure.Providers.Sources.OpenLigaDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Formax.Infrastructure.Providers.Bootstrap;

/// <summary>
/// GDP provider bootstrap DI kaydı.
/// Onaylı provider'ları etkinleştirir ve başlangıç kayıt katmanını (<see cref="ProviderBootstrap"/>) kaydeder.
/// Program.cs yalnızca bu tek kaydı çağırır; hiçbir provider ayarı Program.cs'te bulunmaz.
/// </summary>
public static class ProviderBootstrapServiceCollectionExtensions
{
    public static IServiceCollection AddProviderBootstrap(this IServiceCollection services)
    {
        // Onaylı provider'ları etkinleştir: OpenLigaDB + Core Free Provider Pack (hepsi ücretsiz/açık).
        services.AddOpenLigaDbProvider();
        services.AddCoreFreeProviders();

        // Başlangıç kayıt katmanı (ayarlar burada; ileride yalnızca bu katman değişir).
        services.TryAddSingleton<ProviderBootstrap>();

        return services;
    }
}
