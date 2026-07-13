using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Configuration;
using Formax.Infrastructure.Providers.Health;
using Formax.Infrastructure.Providers.Sources.Common;

namespace Formax.Infrastructure.Providers.Sources.News;

/// <summary>GDELT (api.gdeltproject.org) — ücretsiz/açık, anahtarsız global haber taraması. Capability: News.</summary>
public sealed class GdeltProvider : FreeHttpProvider, INewsProvider
{
    public GdeltProvider(HttpClient http, IProviderConfigurationService configuration, IProviderHealthService health)
        : base(http, configuration, health) { }

    public override ProviderManifest Manifest { get; } = ProviderManifest.Create(
        name: "gdelt",
        capabilities: new[] { ProviderCapability.News },
        category: ProviderCategory.Free);

    public Task<ProviderResult> FetchNewsAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => GetAsync(ProviderCapability.News, config =>
        {
            var query = Param(config, "query");
            if (string.IsNullOrWhiteSpace(query))
                return null;
            return $"{config.BaseUrl}?query={Uri.EscapeDataString(query)}&mode=artlist&format=json&maxrecords=25";
        }, cancellationToken);
}
