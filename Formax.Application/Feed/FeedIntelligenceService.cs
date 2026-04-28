using Formax.Application.DTOs.Recommendations;
using Formax.Application.Services.Intelligence;

namespace Formax.Application.Services.Feed;

public class FeedIntelligenceService
{
    private readonly UserBehaviorService _userBehaviorService;
    private readonly SessionBehaviorService _sessionService;
    private readonly TrendWeightService _trendWeightService;
    private readonly TrendDecayService _trendDecayService;

    public FeedIntelligenceService(
        UserBehaviorService userBehaviorService,
        SessionBehaviorService sessionService,
        TrendWeightService trendWeightService,
        TrendDecayService trendDecayService)
    {
        _userBehaviorService = userBehaviorService;
        _sessionService = sessionService;
        _trendWeightService = trendWeightService;
        _trendDecayService = trendDecayService;
    }

    public async Task Apply(List<RecommendationCardDto> items, int userId)
    {
        var userBias = await _userBehaviorService.GetUserPlayBias(userId);
        var session = await _sessionService.GetSessionState(userId);

        foreach (var item in items)
        {
            item.Priority = 0;

            if (userBias > 0.7 || userBias < 0.3)
                item.Priority += 3;

            double sessionBoost = 0;

            if (session.Mode == "AGGRESSIVE")
                sessionBoost = (1 - item.ConfidenceScore) * 10;
            else if (session.Mode == "CAUTIOUS")
                sessionBoost = item.ConfidenceScore * 10;

            item.Score += sessionBoost;

            var combinedTrend =
                (item.MarketTrendScore * 0.7) +
                (item.UserTrendScore * 0.3);

            var trendWeight = _trendWeightService.CalculateWeight(
                session.Mode ?? "NORMAL",
                "TEMP",
                combinedTrend
            );

            var trendImpact = Math.Clamp(
                combinedTrend * trendWeight * 10,
                0,
                15
            );

            item.Score += trendImpact;

            // 🔥 ASLA DOKUNMA
            // External ❌
            // ExternalTrend ❌
            // ConfidenceLabel ❌

            item.Score = Math.Clamp(item.Score, 0, 100);

            item.TrendWeight = trendWeight;
            item.TrendImpact = combinedTrend * trendWeight;
        }

        items.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }
}