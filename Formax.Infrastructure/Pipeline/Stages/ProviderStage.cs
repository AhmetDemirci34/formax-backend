using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Pipeline.Abstractions;
using Formax.Infrastructure.Providers;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Context;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Pipeline.Stages;

/// <summary>
/// 1. aşama — Provider. Önce <see cref="IMatchContextResolver"/> ile gerçek Match'ten
/// sağlayıcı isteği (takım/lig/koordinat) çözülür; ardından <see cref="ProviderOrchestrator"/> ile
/// kayıtlı sağlayıcılardan HAM veri toplanır. Sağlayıcılar sabit koordinat/takım/venue KULLANMAZ.
///
/// Kayıtlı sağlayıcı yoksa koleksiyonlar boş döner (Orchestrator izole/paralel; bir sağlayıcı hatası
/// aşamayı bozmaz). Çözümlenen istek bağlama yazılır (Persist kanonik koordinatı buradan alır).
/// </summary>
public sealed class ProviderStage : IPipelineStage
{
    private readonly ProviderOrchestrator _orchestrator;
    private readonly IMatchContextResolver _matchContextResolver;
    private readonly ILogger<ProviderStage> _logger;

    public ProviderStage(
        ProviderOrchestrator orchestrator,
        IMatchContextResolver matchContextResolver,
        ILogger<ProviderStage> logger)
    {
        _orchestrator = orchestrator;
        _matchContextResolver = matchContextResolver;
        _logger = logger;
    }

    public PipelineStage Stage => PipelineStage.Provider;

    public async Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        // Gerçek Match bağlamını çöz (MatchId varsa). Yoksa yalnızca FormaxMatchId taşıyan minimal istek.
        var request = context.MatchId is int matchId
            ? await _matchContextResolver.ResolveAsync(matchId, cancellationToken).ConfigureAwait(false)
            : new ProviderRequest { FormaxMatchId = context.FormaxMatchId };

        // Çözümlenen istek sonraki aşamalar için (özellikle Persist'in kanonik koordinatı) bağlama yazılır.
        context.Set("gdp.request", request);

        _logger.LogInformation(
            "ProviderStage: matchId={MatchId} home={Home} away={Away} competition={Competition} coords=({Lat},{Lon})",
            context.MatchId, request.HomeTeam, request.AwayTeam, request.Competition, request.Latitude, request.Longitude);

        var fixtures = await _orchestrator
            .CollectFixturesAsync(request, cancellationToken).ConfigureAwait(false);

        var standings = await _orchestrator
            .CollectAsync<IStandingsProvider>((p, ct) => p.FetchStandingsAsync(request, ct), cancellationToken)
            .ConfigureAwait(false);

        var teams = await _orchestrator
            .CollectAsync<ITeamProvider>((p, ct) => p.FetchTeamsAsync(request, ct), cancellationToken)
            .ConfigureAwait(false);

        // Weather — gerçek maç koordinatıyla. Koordinat çözülemediyse sağlayıcılar veri döndürmez (sahte yok).
        var weather = await _orchestrator
            .CollectAsync<IWeatherProvider>((p, ct) => p.FetchWeatherAsync(request, ct), cancellationToken)
            .ConfigureAwait(false);

        context.Set("gdp.raw.fixtures", fixtures);
        context.Set("gdp.raw.standings", standings);
        context.Set("gdp.raw.teams", teams);
        context.Set("gdp.raw.weather", weather);

        _logger.LogInformation(
            "ProviderStage tamamlandı: fixtures={Fixtures} standings={Standings} teams={Teams} weather={Weather}",
            fixtures.Count, standings.Count, teams.Count, weather.Count);
    }
}
