using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Formax.Infrastructure.Providers.Configuration;

/// <summary>
/// Merkezi Provider Configuration DI kayıtları.
/// Registry ve servis singleton'dır (uygulama boyunca paylaşılan yapılandırma için).
/// Bu faz hiçbir yapılandırma yüklemez; kayıtlar ancak dışarıdan (ileride IConfiguration/panel)
/// <see cref="IProviderConfigurationService.Load"/> ile beslendikçe oluşur.
/// </summary>
public static class ProviderConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddProviderConfiguration(this IServiceCollection services)
    {
        services.TryAddSingleton<ProviderConfigurationRegistry>();
        services.TryAddSingleton<IProviderConfigurationService, ProviderConfigurationService>();
        return services;
    }
}
