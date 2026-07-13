using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Formax.Infrastructure.Providers.Health;

/// <summary>
/// Merkezi Provider Health DI kayıtları.
/// Registry ve servis singleton'dır (uygulama boyunca paylaşılan sağlık durumu için).
/// Bu faz hiçbir sağlık verisi üretmez; kayıtlar ancak dışarıdan (ör. ileride Orchestrator)
/// olay kaydedildikçe oluşur.
/// </summary>
public static class ProviderHealthServiceCollectionExtensions
{
    public static IServiceCollection AddProviderHealth(this IServiceCollection services)
    {
        services.TryAddSingleton<ProviderHealthRegistry>();
        services.TryAddSingleton<IProviderHealthService, ProviderHealthService>();
        return services;
    }
}
