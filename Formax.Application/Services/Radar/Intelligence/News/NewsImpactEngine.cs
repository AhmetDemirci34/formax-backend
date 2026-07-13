using System;
using System.Collections.Generic;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.3) — default impact engine. Each category count is
    /// multiplied by its weight; the sum is clamped to 0-100 and banded. Injury/
    /// Suspension dominate; transfers contribute little. Fully deterministic; no AI.
    /// </summary>
    public sealed class NewsImpactEngine : INewsImpactEngine
    {
        private static readonly IReadOnlyDictionary<NewsCategory, double> Weights =
            new Dictionary<NewsCategory, double>
            {
                [NewsCategory.Injury] = 30,
                [NewsCategory.Suspension] = 25,
                [NewsCategory.Lineup] = 20,
                [NewsCategory.Coach] = 15,
                [NewsCategory.Transfer] = 10,
                [NewsCategory.MatchPreview] = 5,
                [NewsCategory.MatchResult] = 5,
                [NewsCategory.General] = 1,
            };

        public NewsImpactResult Evaluate(IReadOnlyDictionary<NewsCategory, int> categoryCounts)
        {
            if (categoryCounts is null || categoryCounts.Count == 0)
                return new NewsImpactResult
                {
                    Score = NewsImpactScore.Of(0, NewsImpactLevel.Low),
                    RawTotal = 0
                };

            double raw = 0;
            var contributions = new Dictionary<string, double>();

            foreach (var (category, count) in categoryCounts)
            {
                if (count <= 0) continue;
                var weight = Weights.TryGetValue(category, out var w) ? w : 0;
                var points = weight * count;
                raw += points;
                if (points > 0)
                    contributions[category.ToString()] = points;
            }

            var score = Math.Clamp(raw, 0, 100);
            var level = Band(score);

            return new NewsImpactResult
            {
                Score = NewsImpactScore.Of(Math.Round(score, 1), level),
                RawTotal = Math.Round(raw, 1),
                Contributions = contributions
            };
        }

        private static NewsImpactLevel Band(double score) => score switch
        {
            > 75 => NewsImpactLevel.Critical,
            > 50 => NewsImpactLevel.High,
            > 25 => NewsImpactLevel.Medium,
            _ => NewsImpactLevel.Low
        };
    }
}
