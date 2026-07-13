using Formax.Infrastructure.Merge.Abstractions;
using Formax.Infrastructure.Merge.Strategies;
using Formax.Infrastructure.Normalize.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Formax.Infrastructure.Merge;

/// <summary>
/// Merge Engine DI kayıtları.
///
/// Bir model türü için merge stratejisi eklemek = (1) <see cref="IMergeStrategy{T}"/> uygulayan sınıf yaz,
/// (2) tek satırla kaydet: <c>services.AddMergeStrategy&lt;NormalizedFixture, FixtureMergeStrategy&gt;();</c>
/// Engine onu otomatik çözer.
/// </summary>
public static class MergeServiceCollectionExtensions
{
    public static IServiceCollection AddMergeEngine(this IServiceCollection services)
    {
        services.AddScoped<MergeEngine>();

        // Fikstür merge stratejisi (aynı FORMAX Match altındaki NormalizedFixture'ları alan bazlı birleştirir)
        services.AddMergeStrategy<NormalizedFixture, FixtureMergeStrategy>();

        // Weather merge stratejisi (aynı koordinattaki NormalizedWeather'ları alan bazlı birleştirir)
        services.AddMergeStrategy<NormalizedWeather, WeatherMergeStrategy>();

        return services;
    }

    /// <summary>Belirli bir model türü için merge stratejisi kaydeder.</summary>
    public static IServiceCollection AddMergeStrategy<T, TStrategy>(this IServiceCollection services)
        where TStrategy : class, IMergeStrategy<T>
    {
        services.AddScoped<IMergeStrategy<T>, TStrategy>();
        return services;
    }
}
