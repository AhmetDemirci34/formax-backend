using Microsoft.Extensions.DependencyInjection;

namespace Formax.Infrastructure.Normalize.Mapping;

/// <summary>
/// Provider Mapper DI kayıtları.
///
/// Yeni bir provider için mapper eklemek = (1) <see cref="IProviderMapper{TRaw}"/> uygulayan sınıf yaz,
/// (2) tek satırla kaydet: <c>services.AddProviderMapper&lt;RawFixture, OpenLigaDbFixtureMapper&gt;();</c>
/// Aynı ortak HAM model için sınırsız provider mapper'ı kaydedilebilir (her provider bir tane).
/// Bu revizyonda hiçbir somut mapper kaydedilmez (gerçek implementasyon sonraki fazda).
/// </summary>
public static class ProviderMapperServiceCollectionExtensions
{
    public static IServiceCollection AddProviderMapper<TRaw, TMapper>(this IServiceCollection services)
        where TMapper : class, IProviderMapper<TRaw>
    {
        services.AddScoped<IProviderMapper<TRaw>, TMapper>();
        return services;
    }
}
