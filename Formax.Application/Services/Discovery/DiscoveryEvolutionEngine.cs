using Formax.Application.Services.Recommendation;

namespace Formax.Application.Services.Discovery;

public class DiscoveryEvolutionEngine
{
    private readonly DiscoverySessionMemoryService _sessionMemory;
    private readonly InterestReinforcementService _reinforcement;

    public DiscoveryEvolutionEngine(
        DiscoverySessionMemoryService sessionMemory,
        InterestReinforcementService reinforcement)
    {
        _sessionMemory = sessionMemory;
        _reinforcement = reinforcement;
    }

    public double CalculateAdaptiveScore(
        int userId,
        RankingInput match,
        double baseScore,
        string lastEventType,
        double dwellSeconds)
    {
        // kullanıcı bu maçı zaten gördüyse düşür
        if (_sessionMemory.HasSeen(userId, match.MatchId))
            baseScore *= 0.6;

        // davranış boost
        var boost = _reinforcement.CalculateBoost(lastEventType, dwellSeconds);

        var adaptiveScore = baseScore + boost;

        return adaptiveScore;
    }
}
