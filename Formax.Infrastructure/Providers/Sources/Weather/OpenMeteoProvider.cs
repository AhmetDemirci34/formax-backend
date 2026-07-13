using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Configuration;
using Formax.Infrastructure.Providers.Health;
using Formax.Infrastructure.Providers.Sources.Common;

namespace Formax.Infrastructure.Providers.Sources.Weather;

/// <summary>Open-Meteo (api.open-meteo.com) — ücretsiz/açık, anahtarsız. Capability: Weather.</summary>
public sealed class OpenMeteoProvider : FreeHttpProvider, IWeatherProvider
{
    public OpenMeteoProvider(HttpClient http, IProviderConfigurationService configuration, IProviderHealthService health)
        : base(http, configuration, health) { }

    public override ProviderManifest Manifest { get; } = ProviderManifest.Create(
        name: "open-meteo",
        capabilities: new[] { ProviderCapability.Weather },
        category: ProviderCategory.Free);

    public Task<ProviderResult> FetchWeatherAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => GetAsync(ProviderCapability.Weather, config =>
        {
            // Koordinat GERÇEK maçtan (Match Context Resolver) gelir — sabit config koordinatı KULLANILMAZ.
            if (request.Latitude is null || request.Longitude is null)
                return null;

            var lat = request.Latitude.Value.ToString(CultureInfo.InvariantCulture);
            var lon = request.Longitude.Value.ToString(CultureInfo.InvariantCulture);
            return $"{config.BaseUrl}?latitude={lat}&longitude={lon}&hourly=temperature_2m,precipitation,weather_code,wind_speed_10m&forecast_days=1&timezone=UTC";
        }, cancellationToken);
}
