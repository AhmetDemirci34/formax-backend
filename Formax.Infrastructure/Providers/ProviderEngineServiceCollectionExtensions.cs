using Formax.Infrastructure.Providers.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Formax.Infrastructure.Providers;

/// <summary>
/// FORMAX GDP Provider Engine için DI kayıtları.
///
/// Yeni bir sağlayıcı eklemek = (1) <see cref="IDataProvider"/> uygulayan bir sınıf yaz,
/// (2) tek satırla kaydet: <c>services.AddProvider&lt;MyProvider&gt;();</c>
/// Registry ve Orchestrator onu otomatik keşfeder; başka hiçbir yeri değiştirmen gerekmez.
/// </summary>
public static class ProviderEngineServiceCollectionExtensions
{
    /// <summary>Provider Engine çekirdeğini (Registry + Orchestrator) kaydeder.</summary>
    public static IServiceCollection AddProviderEngine(this IServiceCollection services)
    {
        services.AddScoped<ProviderRegistry>();
        services.AddScoped<ProviderOrchestrator>();

        // Somut sağlayıcılar buraya (veya herhangi bir modülde) tek satırla eklenir:
        // services.AddProvider<XxxFixtureProvider>();
        // services.AddProvider<XxxLiveProvider>();
        // services.AddProvider<XxxNewsProvider>();

        return services;
    }

    /// <summary>
    /// Tek bir sağlayıcıyı kaydeder. Somut tip kendi olarak kaydedilir ve aynı örnek
    /// <see cref="IDataProvider"/> koleksiyonuna iletilir; böylece Registry onu keşfeder,
    /// çift örnek (double-instance) oluşmaz. İstenildiği kadar çağrılabilir — limit yoktur.
    /// </summary>
    public static IServiceCollection AddProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IDataProvider
    {
        services.AddScoped<TProvider>();
        services.AddScoped<IDataProvider>(sp => sp.GetRequiredService<TProvider>());
        return services;
    }
}
