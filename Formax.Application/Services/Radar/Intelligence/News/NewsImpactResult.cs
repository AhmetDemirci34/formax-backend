using System;
using System.Collections.Generic;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.3) — full impact assessment: the normalized
    /// score/level plus the uncapped raw total and per-category point contributions.
    /// </summary>
    public sealed class NewsImpactResult
    {
        public NewsImpactScore Score { get; init; } = NewsImpactScore.Of(0, Domain.Enums.NewsImpactLevel.Low);

        /// <summary>Uncapped sum of category points (before clamp to 100).</summary>
        public double RawTotal { get; init; }

        /// <summary>Category name → points contributed.</summary>
        public IReadOnlyDictionary<string, double> Contributions { get; init; }
            = new Dictionary<string, double>();
    }
}
