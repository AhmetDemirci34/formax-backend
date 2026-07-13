using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Configuration;
using Formax.Infrastructure.Providers.Health;
using Formax.Infrastructure.Providers.Sources.Common;

namespace Formax.Infrastructure.Providers.Sources.Weather;

/// <summary>MET Norway / Yr (api.met.no) — ücretsiz/açık, anahtarsız (User-Agent zorunlu). Capability: Weather.</summary>
public sealed class MetNorwayProvider : FreeHttpProvider, IWeatherProvider
{
    public MetNorwayProvider(HttpClient http, IProviderConfigurationService configuration, IProviderHealthService health)
        : base(http, configuration, health) { }

    public override ProviderManifest Manifest { get; } = ProviderManifest.Create(
        name: "met-norway",
        capabilities: new[] { ProviderCapability.Weather },
        category: ProviderCategory.Free);

    public Task<ProviderResult> FetchWeatherAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => GetAsync(ProviderCapability.Weather, config =>
        {
            var lat = Param(config, "latitude");
            var lon = Param(config, "longitude");
            if (string.IsNullOrWhiteSpace(lat) || string.IsNullOrWhiteSpace(lon))
                return null;
            return $"{config.BaseUrl}?lat={lat}&lon={lon}";
        }, cancellationToken);
}
