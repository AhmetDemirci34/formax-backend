using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Prediction.Ranking.Discovery;

namespace Formax.Infrastructure.Historical.Prediction.Ranking;

/// <summary>Discovery Feed'deki her maç için <see cref="RadarMatchContext"/> kuran paylaşılan servis.</summary>
public interface IRadarContextBuilder
{
    Task<IReadOnlyList<RadarMatchContext>> BuildContextsAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Üst Radar katmanları (Hidden Gems / Daily Picks / API) için TEK bağlam kaynağı. Discovery Feed'i (FAZ 4.3)
/// SIRASINI KORUYARAK kurar ve her maç için Base Radar breakdown'ı (FAZ 4.1) yeniden hesaplar. Mevcut motorlara
/// DOKUNMAZ; yalnız birleştirir. Deterministik. (DRY: HiddenGems/DailyPicks/API bunu paylaşır.)
/// </summary>
public sealed class RadarContextBuilder : IRadarContextBuilder
{
    private readonly IDiscoveryFeedService _discovery;
    private readonly IRadarInputBuilder _inputBuilder;
    private readonly IRadarScoreEngine _radarEngine;
    private readonly RadarWeights _radarWeights;

    public RadarContextBuilder(IDiscoveryFeedService discovery, IRadarInputBuilder inputBuilder, IRadarScoreEngine radarEngine, RadarWeights radarWeights)
    {
        _discovery = discovery;
        _inputBuilder = inputBuilder;
        _radarEngine = radarEngine;
        _radarWeights = radarWeights;
    }

    public async Task<IReadOnlyList<RadarMatchContext>> BuildContextsAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
    {
        var feed = await _discovery.BuildFeedAsync(userId, matchIds, cancellationToken).ConfigureAwait(false);
        var contexts = new List<RadarMatchContext>(feed.Items.Count);

        foreach (var item in feed.Items) // Discovery sırası korunur
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = await _inputBuilder.BuildAsync(item.MatchId, cancellationToken).ConfigureAwait(false);
            if (input is null) continue;

            contexts.Add(new RadarMatchContext
            {
                MatchId = item.MatchId,
                Discovery = item,
                Radar = _radarEngine.Score(input, _radarWeights),
                Probability = input.Probability,
                Confidence = input.Confidence
            });
        }

        return contexts;
    }
}
