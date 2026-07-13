using Formax.Infrastructure.MatchIdentity.Abstractions;
using Formax.Infrastructure.MatchIdentity.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Formax.Infrastructure.MatchIdentity;

/// <summary>
/// Match Identity Engine DI kayıtları.
///
/// Yeni bir eşleştirme stratejisi eklemek = (1) <see cref="IMatchIdentityStrategy"/> uygulayan sınıf yaz,
/// (2) tek satırla kaydet: <c>services.AddMatchIdentityStrategy&lt;MyStrategy&gt;();</c>
/// Engine onu önceliğe göre otomatik çalıştırır.
/// </summary>
public static class MatchIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddMatchIdentityEngine(this IServiceCollection services)
    {
        // Merkezi eşik + ağırlık yapılandırması (ileride appsettings/db/panel'den beslenebilir)
        services.TryAddSingleton<MatchIdentityConfiguration>();

        services.AddScoped<MatchIdentityEngine>();
        services.AddScoped<IMatchIdentityResolver>(sp => sp.GetRequiredService<MatchIdentityEngine>());

        // Eşleştirme kriterleri (her biri kendi skorunu üretir; engine toplar)
        services.AddMatchIdentityStrategy<ProviderIdStrategy>();
        services.AddMatchIdentityStrategy<HomeTeamStrategy>();
        services.AddMatchIdentityStrategy<AwayTeamStrategy>();
        services.AddMatchIdentityStrategy<KickoffTimeStrategy>();
        services.AddMatchIdentityStrategy<CompetitionStrategy>();

        return services;
    }

    /// <summary>
    /// Bir eşleştirme stratejisini kaydeder. İstenildiği kadar çağrılabilir; limit yoktur.
    /// </summary>
    public static IServiceCollection AddMatchIdentityStrategy<TStrategy>(this IServiceCollection services)
        where TStrategy : class, IMatchIdentityStrategy
    {
        services.AddScoped<IMatchIdentityStrategy, TStrategy>();
        return services;
    }
}
