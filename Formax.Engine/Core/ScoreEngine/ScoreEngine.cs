using Formax.Engine.Core.ExternalTrends;

namespace Formax.Engine.Core.Scoring;

public class ScoreEngine
{
    private readonly ExternalTrendEngine _externalTrendEngine;

    public ScoreEngine(ExternalTrendEngine externalTrendEngine)
    {
        _externalTrendEngine = externalTrendEngine;
    }

    public double CalculateScore(int matchId, double baseScore)
    {
        var external = _externalTrendEngine.Calculate(matchId);

        var impact = external.MarketConfidence * 20;

        var score = baseScore + impact;

        if (score > 100) score = 100;
        if (score < 0) score = 0;

        return Math.Round(score, 2);
    }
}