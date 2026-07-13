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
/// Wikidata (wikidata.org) — ücretsiz/açık, anahtarsız. Metadata capability'leri:
/// Team, Player, Coach, Venue, Referee. Her tür aynı wbsearchentities API'sini kullanır.
/// </summary>
public sealed class WikidataProvider : FreeHttpProvider,
    ITeamProvider, IPlayerProvider, ICoachProvider, IVenueProvider, IRefereeProvider
{
    public WikidataProvider(HttpClient http, IProviderConfigurationService configuration, IProviderHealthService health)
        : base(http, configuration, health) { }

    public override ProviderManifest Manifest { get; } = ProviderManifest.Create(
        name: "wikidata",
        capabilities: new[]
        {
            ProviderCapability.Team,
            ProviderCapability.Player,
            ProviderCapability.Coach,
            ProviderCapability.Venue,
            ProviderCapability.Referee
        },
        category: ProviderCategory.Free);

    public Task<ProviderResult> FetchTeamsAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => SearchAsync(ProviderCapability.Team, cancellationToken);

    public Task<ProviderResult> FetchPlayersAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => SearchAsync(ProviderCapability.Player, cancellationToken);

    public Task<ProviderResult> FetchCoachesAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => SearchAsync(ProviderCapability.Coach, cancellationToken);

    public Task<ProviderResult> FetchVenuesAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => SearchAsync(ProviderCapability.Venue, cancellationToken);

    public Task<ProviderResult> FetchRefereesAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => SearchAsync(ProviderCapability.Referee, cancellationToken);

    private Task<ProviderResult> SearchAsync(ProviderCapability capability, CancellationToken cancellationToken)
        => GetAsync(capability, config =>
        {
            var query = Param(config, "query");
            if (string.IsNullOrWhiteSpace(query))
                return null;
            return $"{config.BaseUrl}?action=wbsearchentities&search={Uri.EscapeDataString(query)}&language=en&format=json";
        }, cancellationToken);
}
