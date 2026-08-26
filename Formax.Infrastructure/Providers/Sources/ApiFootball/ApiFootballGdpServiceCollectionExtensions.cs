using Formax.Infrastructure.Normalize.Mapping;
using Formax.Infrastructure.Normalize.Raw;
using Microsoft.Extensions.DependencyInjection;

namespace Formax.Infrastructure.Providers.Sources.ApiFootball;

/// <summary>
/// api-football GDP köprüsünün DI kaydı — mevcut Provider Platform kullanılır (AddProvider /
/// AddProviderMapper); yeni altyapı yoktur.
///
/// Sağlayıcı yeni HttpClient KAYDETMEZ: mevcut <c>ISportsDataProvider</c> (Program.cs'te
/// AddHttpClient ile kayıtlı) sarmalanır → cache/metering/rate-limit korunur.
/// Her ikisi de Scoped olduğundan captive dependency oluşmaz.
/// </summary>
public static class ApiFootballGdpServiceCollectionExtensions
{
    public static IServiceCollection AddApiFootballGdpProvider(this IServiceCollection services)
    {
        services.AddProvider<ApiFootballGdpProvider>();
        services.AddProviderMapper<RawFixture, ApiFootballFixtureMapper>();

        return services;
    }
}
