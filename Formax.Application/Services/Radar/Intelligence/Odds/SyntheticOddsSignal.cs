using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Odds
{
    /// <summary>
    /// Radar Odds Movement (R.11.3) — a synthetic odds signal for a match, derived from
    /// Radar's internal scores (no real odds, no API). SignalScore is the normalized
    /// blend; Level bands it; Direction maps the band onto the odds-movement vocabulary
    /// so it can feed the Odds Movement layer.
    /// </summary>
    public sealed class SyntheticOddsSignal
    {
        public int MatchId { get; init; }

        // ── Inputs (0..100) ───────────────────────────────────────────────────
        public double InterestScore { get; init; }
        public double NewsImpactScore { get; init; }
        public double ImportanceScore { get; init; }

        // ── Outputs ───────────────────────────────────────────────────────────
        /// <summary>Normalized 0..100 blend of the three inputs.</summary>
        public double SignalScore { get; init; }

        public SyntheticOddsLevel Level { get; init; }

        /// <summary>Synthetic odds direction (reuses the Odds Movement vocabulary so it
        /// can feed that layer): Critical/High → Falling, Medium → Stable, Low → Rising.</summary>
        public OddsMovementDirection Direction { get; init; }
    }
}
