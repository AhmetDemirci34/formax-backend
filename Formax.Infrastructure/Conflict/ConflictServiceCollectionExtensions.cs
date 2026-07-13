using System;
using Formax.Infrastructure.Conflict.Abstractions;
using Formax.Infrastructure.Conflict.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Formax.Infrastructure.Conflict;

/// <summary>
/// Conflict Engine DI kayıtları.
///
/// Stratejiler açık-generic kaydedilir; her model türü <typeparamref>T</typeparamref> için otomatik geçerlidir.
/// Yeni bir kriter eklemek = (1) <see cref="IConflictResolver{T}"/> uygulayan açık-generic sınıf yaz,
/// (2) tek satırla kaydet: <c>services.AddConflictResolver(typeof(MyResolver&lt;&gt;));</c>
/// </summary>
public static class ConflictServiceCollectionExtensions
{
    public static IServiceCollection AddConflictEngine(this IServiceCollection services)
    {
        // Yapılandırma KAYNAĞI soyutlama üzerinden (ileride appsettings/db/panel bu sağlayıcıyı değiştirir)
        services.TryAddSingleton<IConflictConfigurationProvider, DefaultConflictConfigurationProvider>();

        services.AddScoped<ConflictEngine>();

        // Kriter stratejileri (Configuration sırasına göre çalıştırılır)
        services.AddConflictResolver(typeof(ManualOverrideConflictResolver<>));
        services.AddConflictResolver(typeof(ProviderConfidenceConflictResolver<>));
        services.AddConflictResolver(typeof(ProviderPriorityConflictResolver<>));
        services.AddConflictResolver(typeof(SourceCountConflictResolver<>));
        services.AddConflictResolver(typeof(LastUpdatedConflictResolver<>));

        return services;
    }

    /// <summary>
    /// Açık-generic bir conflict resolver kaydeder (ör. <c>typeof(MyResolver&lt;&gt;)</c>).
    /// İstenildiği kadar çağrılabilir; limit yoktur.
    /// </summary>
    public static IServiceCollection AddConflictResolver(this IServiceCollection services, Type openGenericResolverType)
    {
        services.AddScoped(typeof(IConflictResolver<>), openGenericResolverType);
        return services;
    }
}
