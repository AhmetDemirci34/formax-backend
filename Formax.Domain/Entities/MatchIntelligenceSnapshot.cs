using System;
using Formax.Domain.Enums;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Radar Match Intelligence (R.9.1) — persisted intelligence result for one match.
    /// The first Intelligence-layer consumer of match data. One row per match
    /// (PK = MatchId). Produced by the pipeline MatchProfile → MatchSignal →
    /// MatchIntelligenceSnapshot.
    ///
    /// Feed / News / Odds / Commentary / AI are NOT part of this sprint; this is the
    /// raw importance-signal core only.
    /// </summary>
    public sealed class MatchIntelligenceSnapshot
    {
        /// <summary>FK + PK → Match.Id.</summary>
        public int MatchId { get; set; }

        public MatchIntelligenceStatus Status { get; set; } = MatchIntelligenceStatus.Unknown;

        /// <summary>Highest-weight signal for the match (None when no signal).</summary>
        public MatchSignalType PrimarySignalType { get; set; } = MatchSignalType.None;

        public int SignalCount { get; set; }

        /// <summary>All derived signals serialized as JSON (MatchSignal[]).</summary>
        public string SignalsJson { get; set; } = "[]";

        /// <summary>Human-readable one-line summary of the signals.</summary>
        public string Summary { get; set; } = string.Empty;

        // ── R.9.6: first-class importance ──────────────────────────────────────
        /// <summary>Normalized 0-100 match importance.</summary>
        public double ImportanceScore { get; set; }

        /// <summary>Banded level derived from <see cref="ImportanceScore"/>.</summary>
        public MatchImportanceLevel ImportanceLevel { get; set; } = MatchImportanceLevel.Low;

        // ── R.10.4: News Intelligence fed into the match snapshot ──────────────
        public double NewsImpactScore { get; set; }
        public NewsImpactLevel NewsImpactLevel { get; set; } = NewsImpactLevel.Low;

        // ── R.11.4: Synthetic Odds signal fed into the match snapshot ──────────
        public double SyntheticSignalScore { get; set; }
        public SyntheticOddsLevel SyntheticSignalLevel { get; set; } = SyntheticOddsLevel.Low;
        public OddsMovementDirection SyntheticDirection { get; set; } = OddsMovementDirection.Stable;

        public DateTime GeneratedAtUtc { get; set; }
    }
}
