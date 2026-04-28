using Formax.Application.DTOs.Home;

namespace Formax.Application.Services.Feed;

public class MatchRankingService
{
    public double CalculateScore(HomeRadarMatchDto m)
    {
        var global = Normalize(m.MatchHeatScore);
        var odds = Normalize(m.OddsMovementScore); // bunu DTO’ya ekleyeceğiz
        var behavior = Normalize(m.BehaviorMomentumScore);
        var freshness = Normalize(m.TimeProximityScore);

        var score =
            (global * 0.40) +
            (odds * 0.25) +
            (behavior * 0.25) +
            (freshness * 0.10);

        return score;
    }

    private double Normalize(double value)
    {
        return Math.Max(0, Math.Min(1, value / 100.0));
    }
}