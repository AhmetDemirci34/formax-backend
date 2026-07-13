using System;
using System.Collections.Generic;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.6) — full importance assessment: the normalized
    /// score/level plus the individual factors that produced it.
    /// </summary>
    public sealed class MatchImportanceResult
    {
        public MatchImportanceScore Score { get; init; } = MatchImportanceScore.Of(0, Domain.Enums.MatchImportanceLevel.Low);

        public IReadOnlyList<MatchImportanceFactor> Factors { get; init; } = Array.Empty<MatchImportanceFactor>();

        /// <summary>Uncapped sum of factor points (before clamp to 100).</summary>
        public double RawTotal { get; init; }
    }
}
