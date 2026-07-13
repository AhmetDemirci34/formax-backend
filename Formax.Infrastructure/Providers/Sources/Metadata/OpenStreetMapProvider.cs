using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Configuration;
using Formax.Infrastructure.Providers.Health;
using Formax.Infrastructure.Providers.Sources.Common;

namespace Formax.Infrastructure.Providers.Sources.Metadata;

/// <summary>
/// OpenStreetMap / Nominatim (nominatim.openstreetmap.org) — ücretsiz/açık, anahtarsız (User-Agent zorunlu).
/// Capability: Venue (stat → koordinat; Weather zincirini besler).
/// </summary>
public sealed class OpenStreetMapProvider : FreeHttpProvider, IVenueProvider
{
    public OpenStreetMapProvider(HttpClient http, IProviderConfigurationService configuration, IProviderHealthService health)
        : base(http, configuration, health) { }

    public override ProviderManifest Manifest { get; } = ProviderManifest.Create(
        name: "openstreetmap",
        capabilities: new[] { ProviderCapability.Venue },
        category: ProviderCategory.Free);

    public Task<ProviderResult> FetchVenuesAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => GetAsync(ProviderCapability.Venue, config =>
        {
            var venue = Param(config, "venue");
            if (string.IsNullOrWhiteSpace(venue))
                return null;
            return $"{config.BaseUrl}?q={Uri.EscapeDataString(venue)}&format=json&limit=1";
        }, cancellationToken);
}
