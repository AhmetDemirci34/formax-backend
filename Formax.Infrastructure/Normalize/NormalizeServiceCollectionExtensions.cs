using Formax.Infrastructure.Normalize.Abstractions;
using Formax.Infrastructure.Normalize.Geo;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Normalizers;
using Formax.Infrastructure.Normalize.Raw;
using Microsoft.Extensions.DependencyInjection;

namespace Formax.Infrastructure.Normalize;

/// <summary>
/// Normalize Engine DI kayıtları.
///
/// Normalizer eklemek = (1) <see cref="INormalizer{TRaw, TModel}"/> uygulayan sınıf yaz,
/// (2) tek satırla kaydet: <c>services.AddNormalizer&lt;RawFixture, NormalizedFixture, FixtureNormalizer&gt;();</c>
/// Her (HAM, Normalized) çifti için TEK normalizer vardır (provider-bağımsız).
/// </summary>
public static class NormalizeServiceCollectionExtensions
{
    public static IServiceCollection AddNormalizeEngine(this IServiceCollection services)
    {
        services.AddScoped<NormalizeEngine>();
        services.AddSingleton<ICountryNormalizer, CountryNormalizer>();

        // İskelet normalizer'lar (ortak HAM → Normalized; gerçek eşleme sonraki fazda)
        services.AddNormalizer<RawFixture, NormalizedFixture, FixtureNormalizer>();
        services.AddNormalizer<RawTeam, NormalizedTeam, TeamNormalizer>();
        services.AddNormalizer<RawCompetition, NormalizedCompetition, CompetitionNormalizer>();
        services.AddNormalizer<RawPlayer, NormalizedPlayer, PlayerNormalizer>();
        services.AddNormalizer<RawVenue, NormalizedVenue, VenueNormalizer>();
        services.AddNormalizer<RawWeather, NormalizedWeather, WeatherNormalizer>();

        return services;
    }

    /// <summary>Bir (HAM, Normalized) çifti için normalizer kaydeder.</summary>
    public static IServiceCollection AddNormalizer<TRaw, TModel, TNormalizer>(this IServiceCollection services)
        where TNormalizer : class, INormalizer<TRaw, TModel>
    {
        services.AddScoped<INormalizer<TRaw, TModel>, TNormalizer>();
        return services;
    }
}
