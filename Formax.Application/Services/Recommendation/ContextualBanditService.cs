using System;

namespace Formax.Application.Services.Recommendation;

public class ContextualBanditService
{
    private const double EPSILON = 0.15; // %15 exploration

    public double CalculateBanditScore(
        double clicks,     // 🔥 FIX → double
        double opens,      // 🔥 FIX
        double follows,    // 🔥 FIX
        int impressions,
        int totalImpressions)
    {
        if (impressions <= 0) impressions = 1;
        if (totalImpressions <= 0) totalImpressions = 1;

        // 🔥 REWARD (WEIGHTED)
        double reward =
            (clicks * 3.0) +
            (opens * 6.0) +
            (follows * 8.0);

        // 🔥 EXPLOITATION (CTR BENZERİ)
        double exploitation = reward / impressions;

        // 🔥 EXPLORATION (UCB - STABLE)
        double exploration = Math.Sqrt(
            Math.Log(totalImpressions + 1) / impressions
        );

        double score = exploitation + exploration;

        // 🔥 EPSILON GREEDY (CONTROLLED RANDOMNESS)
        if (Random.Shared.NextDouble() < EPSILON)
        {
            score += Random.Shared.NextDouble() * 3.0; // 🔥 daha doğal boost
        }

        return score;
    }
}