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
/// Wikipedia REST (en.wikipedia.org/api/rest_v1) — ücretsiz/açık, anahtarsız.
/// Metadata capability'leri: Team, Player (özet/metadata).
/// </summary>
public sealed class WikipediaProvider : FreeHttpProvider, ITeamProvider, IPlayerProvider
{
    public WikipediaProvider(HttpClient http, IProviderConfigurationService configuration, IProviderHealthService health)
        : base(http, configuration, health) { }

    public override ProviderManifest Manifest { get; } = ProviderManifest.Create(
        name: "wikipedia",
        capabilities: new[] { ProviderCapability.Team, ProviderCapability.Player },
        category: ProviderCategory.Free);

    public Task<ProviderResult> FetchTeamsAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => SummaryAsync(ProviderCapability.Team, cancellationToken);

    public Task<ProviderResult> FetchPlayersAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => SummaryAsync(ProviderCapability.Player, cancellationToken);

    private Task<ProviderResult> SummaryAsync(ProviderCapability capability, CancellationToken cancellationToken)
        => GetAsync(capability, config =>
        {
            var title = Param(config, "title");
            if (string.IsNullOrWhiteSpace(title))
                return null;
            return $"{config.BaseUrl!.TrimEnd('/')}/page/summary/{Uri.EscapeDataString(title)}";
        }, cancellationToken);
}
