using System.Collections.Generic;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.2) — a coarse importance assessment built from
    /// ranks and club stature. Score is internal context (not shown to users, not yet
    /// a feed signal).
    /// </summary>
    public sealed class MatchImportanceContext
    {
        /// <summary>0..100 composite importance.</summary>
        public double ImportanceScore { get; init; }

        public bool RanksClose { get; init; }
        public bool BothTopTier { get; init; }
        public bool HasStableClubs { get; init; }

        /// <summary>Deterministic explanations of the contributing factors.</summary>
        public IReadOnlyList<string> Factors { get; init; } = new List<string>();
    }
}
