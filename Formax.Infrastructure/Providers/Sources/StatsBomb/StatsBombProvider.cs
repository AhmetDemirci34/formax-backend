using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Configuration;
using Formax.Infrastructure.Providers.Health;
using Formax.Infrastructure.Providers.Sources.Common;

namespace Formax.Infrastructure.Providers.Sources.StatsBomb;

/// <summary>
/// StatsBomb Open Data (github.com/statsbomb/open-data) — ücretsiz/açık JSON (anahtarsız).
/// Capability: Fixture (tarihsel maçlar). JSON → RawFixture dönüşümü <see cref="StatsBombFixtureMapper"/>'da.
/// </summary>
public sealed class StatsBombProvider : FreeHttpProvider, IFixtureProvider
{
    public StatsBombProvider(HttpClient http, IProviderConfigurationService configuration, IProviderHealthService health)
        : base(http, configuration, health) { }

    public override ProviderManifest Manifest { get; } = ProviderManifest.Create(
        name: "statsbomb",
        capabilities: new[] { ProviderCapability.Fixture },
        category: ProviderCategory.Free);

    public Task<ProviderResult> FetchFixturesAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => GetAsync(ProviderCapability.Fixture, config =>
        {
            var competitionId = Param(config, "competitionId");
            var seasonId = Param(config, "seasonId");
            if (string.IsNullOrWhiteSpace(competitionId) || string.IsNullOrWhiteSpace(seasonId))
                return null;
            return $"{config.BaseUrl!.TrimEnd('/')}/{competitionId}/{seasonId}.json";
        }, cancellationToken);
}
