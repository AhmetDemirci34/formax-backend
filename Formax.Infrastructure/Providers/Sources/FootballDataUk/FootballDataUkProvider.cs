using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Configuration;
using Formax.Infrastructure.Providers.Health;
using Formax.Infrastructure.Providers.Sources.Common;

namespace Formax.Infrastructure.Providers.Sources.FootballDataUk;

/// <summary>
/// Football-Data.co.uk — ücretsiz/açık CSV indirmesi (anahtarsız). Capability: Fixture (tarihsel sonuçlar).
/// CSV → RawFixture dönüşümü <see cref="FootballDataUkFixtureMapper"/>'da yapılır.
/// </summary>
public sealed class FootballDataUkProvider : FreeHttpProvider, IFixtureProvider
{
    public FootballDataUkProvider(HttpClient http, IProviderConfigurationService configuration, IProviderHealthService health)
        : base(http, configuration, health) { }

    public override ProviderManifest Manifest { get; } = ProviderManifest.Create(
        name: "football-data-uk",
        capabilities: new[] { ProviderCapability.Fixture },
        category: ProviderCategory.Free);

    public Task<ProviderResult> FetchFixturesAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => GetAsync(ProviderCapability.Fixture, config =>
        {
            var season = Param(config, "season");
            var league = Param(config, "league");
            if (string.IsNullOrWhiteSpace(season) || string.IsNullOrWhiteSpace(league))
                return null;
            return $"{config.BaseUrl!.TrimEnd('/')}/mmz4281/{season}/{league}.csv";
        }, cancellationToken);
}
