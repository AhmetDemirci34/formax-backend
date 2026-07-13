using System;
using Formax.Application.DTOs.Radar;
using Microsoft.Extensions.Options;

namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.5) — default ranking support service.
    ///
    /// RadarScore core = ImportanceScore (which already blends News + Synthetic factors,
    /// per R.10.4 / R.11.4), plus a small tone/signal bonus. Blended sort key:
    ///   final = rec × (1 - influence) + (radar/100) × influence
    /// Influence is hard-capped at 0.15 so Radar stays a support layer.
    /// </summary>
    public sealed class RadarRankingService : IRadarRankingService
    {
        private const double MaxInfluence = 0.15;

        private readonly RadarRankingOptions _options;

        public RadarRankingService(IOptions<RadarRankingOptions> options)
        {
            _options = options.Value;
        }

        public bool Enabled => _options.Enabled;

        public double NormalizeRadarScore(FeedInsightDto insight)
        {
            // ImportanceScore is already 0-100 and encodes news/synthetic contributions.
            var score = insight.ImportanceScore;

            // Small, deterministic bonuses (kept tiny — support signal only).
            score += insight.CommentaryTone switch
            {
                "Critical" => 5,
                "Important" => 2,
                _ => 0
            };

            score += insight.PrimarySignal switch
            {
                "Derby" => 3,
                "Final" => 3,
                _ => 0
            };

            return Math.Clamp(score, 0, 100);
        }

        public double ComputeFinalScore(double recommendationScore01, double radarScore0to100)
        {
            if (!_options.Enabled)
                return recommendationScore01;

            var influence = Math.Clamp(_options.Influence, 0, MaxInfluence);
            return recommendationScore01 * (1 - influence)
                 + (radarScore0to100 / 100.0) * influence;
        }
    }
}
