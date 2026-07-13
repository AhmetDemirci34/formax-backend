using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.5) — turns a Radar insight into a 0-100 support score and blends
    /// it into the existing recommendation score at a low, capped influence. It never
    /// replaces RecommendationScore/UCB/Bandit — only nudges the sort key.
    /// </summary>
    public interface IRadarRankingService
    {
        bool Enabled { get; }

        /// <summary>Normalize a Radar insight to a 0-100 support score.</summary>
        double NormalizeRadarScore(FeedInsightDto insight);

        /// <summary>
        /// Blend the existing recommendation score (0..1) with the Radar score (0..100)
        /// at the capped influence. Returns the existing score unchanged when disabled.
        /// </summary>
        double ComputeFinalScore(double recommendationScore01, double radarScore0to100);
    }
}
