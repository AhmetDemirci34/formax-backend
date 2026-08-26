using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Prediction.Ranking.DailyPicks;
using Formax.Infrastructure.Historical.Prediction.Ranking.HiddenGems;
using Formax.Infrastructure.Historical.Prediction.Ranking.Recommendation;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Api;

/// <summary>Radar API'nin tüm görünümlerini üreten sorgu servisi (mevcut motorları birleştirir; hiçbirini değiştirmez).</summary>
public interface IRadarQueryService
{
    Task<IReadOnlyList<RadarMatchDto>> GetRadarAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);
    Task<DailyPicksResult> GetDailyAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HiddenGemAnalysis>> GetHiddenGemsAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchRecommendation>> GetRecommendationsAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IRadarQueryService"/>
public sealed class RadarQueryService : IRadarQueryService
{
    private readonly IRadarContextBuilder _contextBuilder;
    private readonly IMatchRecommendationEngine _recommendationEngine;
    private readonly IHiddenGemsEngine _hiddenGemsEngine;
    private readonly IDailyPicksService _dailyPicks;
    private readonly IHiddenGemsService _hiddenGems;
    private readonly IMatchRecommendationService _recommendations;
    private readonly RecommendationWeights _recWeights;
    private readonly HiddenGemWeights _gemWeights;

    public RadarQueryService(
        IRadarContextBuilder contextBuilder,
        IMatchRecommendationEngine recommendationEngine,
        IHiddenGemsEngine hiddenGemsEngine,
        IDailyPicksService dailyPicks,
        IHiddenGemsService hiddenGems,
        IMatchRecommendationService recommendations,
        RecommendationWeights recWeights,
        HiddenGemWeights gemWeights)
    {
        _contextBuilder = contextBuilder;
        _recommendationEngine = recommendationEngine;
        _hiddenGemsEngine = hiddenGemsEngine;
        _dailyPicks = dailyPicks;
        _hiddenGems = hiddenGems;
        _recommendations = recommendations;
        _recWeights = recWeights;
        _gemWeights = gemWeights;
    }

    public async Task<IReadOnlyList<RadarMatchDto>> GetRadarAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
    {
        var contexts = await _contextBuilder.BuildContextsAsync(userId, matchIds, cancellationToken).ConfigureAwait(false);
        return contexts.Select(c =>
        {
            var rec = _recommendationEngine.Recommend(new RecommendationInput
            {
                MatchId = c.MatchId, Discovery = c.Discovery, Radar = c.Radar, Probability = c.Probability, Confidence = c.Confidence
            }, _recWeights);
            var gem = _hiddenGemsEngine.Analyze(c, _gemWeights);
            var p = c.Probability;

            return new RadarMatchDto
            {
                MatchId = c.MatchId,
                RadarScore = c.Radar.Score,
                PersonalScore = c.Discovery.PersonalScore,
                DiscoveryScore = c.Discovery.DiscoveryScore,
                DiscoveryRank = c.Discovery.DiscoveryRank,
                HiddenGem = gem.IsHiddenGem,
                HiddenGemScore = gem.HiddenGemScore,
                ConfidenceScore = c.Confidence.ConfidenceScore,
                ConfidenceLevel = c.Confidence.Level.ToString(),
                Probability = new ProbabilityDto { HomeWin = p.Length > 0 ? p[0] : 0, Draw = p.Length > 1 ? p[1] : 0, AwayWin = p.Length > 2 ? p[2] : 0 },
                Recommendation = new RecommendationSummaryDto { Score = rec.RecommendationScore, Level = rec.Level.ToString(), Tags = rec.Tags, Reasons = rec.Reasons },
                Breakdown = c.Radar.Breakdown
            };
        }).ToList();
    }

    public Task<DailyPicksResult> GetDailyAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
        => _dailyPicks.GetDailyPicksAsync(userId, matchIds, cancellationToken);

    public Task<IReadOnlyList<HiddenGemAnalysis>> GetHiddenGemsAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
        => _hiddenGems.FindHiddenGemsAsync(userId, matchIds, cancellationToken);

    public Task<IReadOnlyList<MatchRecommendation>> GetRecommendationsAsync(int? userId, IReadOnlyCollection<int> matchIds, CancellationToken cancellationToken = default)
        => _recommendations.RecommendAsync(userId, matchIds, cancellationToken);
}
