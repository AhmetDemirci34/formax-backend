using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Configuration;
using Formax.Infrastructure.Providers.Health;
using Formax.Infrastructure.Providers.Sources.Common;

namespace Formax.Infrastructure.Providers.Sources.News;

/// <summary>RSS haber akışı (yayıncı RSS/Atom — ör. BBC Sport) — ücretsiz, anahtarsız. Capability: News.</summary>
public sealed class RssNewsProvider : FreeHttpProvider, INewsProvider
{
    public RssNewsProvider(HttpClient http, IProviderConfigurationService configuration, IProviderHealthService health)
        : base(http, configuration, health) { }

    public override ProviderManifest Manifest { get; } = ProviderManifest.Create(
        name: "rss",
        capabilities: new[] { ProviderCapability.News },
        category: ProviderCategory.Free);

    public Task<ProviderResult> FetchNewsAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => GetAsync(ProviderCapability.News, config => config.BaseUrl, cancellationToken);
}
