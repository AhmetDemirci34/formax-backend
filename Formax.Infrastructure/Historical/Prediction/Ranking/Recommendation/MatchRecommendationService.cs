using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Prediction.Ranking.Discovery;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Recommendation;

/// <summary>Discovery Feed'deki maçlar için öneri (açıklama) üreten servis sözleşmesi.</summary>
public interface IMatchRecommendationService
{
    Task<IReadOnlyList<MatchRecommendation>> RecommendAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Recommendation orkestrasyonu: Discovery Feed'i (FAZ 4.3) kurar ve SIRASINI KORUYARAK her maç için Base Radar
/// breakdown'ı (FAZ 4.1) yeniden hesaplayıp öneri üretir. Discovery sıralamasını DEĞİŞTİRMEZ; yalnız açıklama/
/// öneri ekler. Radar/Personal/Discovery motorlarına DOKUNMAZ. Deterministik.
/// </summary>
public sealed class MatchRecommendationService : IMatchRecommendationService
{
    private readonly IDiscoveryFeedService _discovery;
    private readonly IRadarInputBuilder _inputBuilder;
    private readonly IRadarScoreEngine _radarEngine;
    private readonly IMatchRecommendationEngine _recommendationEngine;
    private readonly RadarWeights _radarWeights;
    private readonly RecommendationWeights _recommendationWeights;

    public MatchRecommendationService(
        IDiscoveryFeedService discovery,
        IRadarInputBuilder inputBuilder,
        IRadarScoreEngine radarEngine,
        IMatchRecommendationEngine recommendationEngine,
        RadarWeights radarWeights,
        RecommendationWeights recommendationWeights)
    {
        _discovery = discovery;
        _inputBuilder = inputBuilder;
        _radarEngine = radarEngine;
        _recommendationEngine = recommendationEngine;
        _radarWeights = radarWeights;
        _recommendationWeights = recommendationWeights;
    }

    public async Task<IReadOnlyList<MatchRecommendation>> RecommendAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
    {
        var feed = await _discovery.BuildFeedAsync(userId, matchIds, cancellationToken).ConfigureAwait(false);

        var recommendations = new List<MatchRecommendation>(feed.Items.Count);
        foreach (var item in feed.Items) // Discovery SIRASI korunur → sıralama bozulmaz
        {
            cancellationToken.ThrowIfCancellationRequested();

            var input = await _inputBuilder.BuildAsync(item.MatchId, cancellationToken).ConfigureAwait(false);
            if (input is null) continue;

            var radar = _radarEngine.Score(input, _radarWeights); // Base Radar Score — değişmez

            recommendations.Add(_recommendationEngine.Recommend(new RecommendationInput
            {
                MatchId = item.MatchId,
                Discovery = item,
                Radar = radar,
                Probability = input.Probability,
                Confidence = input.Confidence
            }, _recommendationWeights));
        }

        return recommendations; // Discovery sırasıyla
    }
}
