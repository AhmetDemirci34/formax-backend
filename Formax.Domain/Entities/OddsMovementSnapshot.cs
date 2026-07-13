using System;
using Formax.Domain.Enums;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Radar Odds Movement (R.11.1) — the computed movement for a match between two odds
    /// readings (on the home market). One row per match (PK = MatchId). Produced by the
    /// movement engine; not fed to feed/commentary in this sprint.
    /// </summary>
    public sealed class OddsMovementSnapshot
    {
        /// <summary>Surrogate PK. R.11.2 — movements are a per-match time series, so
        /// many rows may exist for one match.</summary>
        public long Id { get; set; }

        /// <summary>FK → Match.Id (indexed, non-unique).</summary>
        public int MatchId { get; set; }

        /// <summary>Earlier home odds.</summary>
        public double PreviousOdds { get; set; }

        /// <summary>Later home odds.</summary>
        public double CurrentOdds { get; set; }

        /// <summary>Absolute change (|current - previous|).</summary>
        public double Delta { get; set; }

        public OddsMovementDirection Direction { get; set; } = OddsMovementDirection.Stable;
        public OddsMovementLevel Level { get; set; } = OddsMovementLevel.Weak;

        public DateTime ComputedAtUtc { get; set; }
    }
}
