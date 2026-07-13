namespace Formax.Application.DTOs.Radar
{
    /// <summary>
    /// Radar Learning (R.14.3) — a user's affinity to one match, combining the user's
    /// interest profile (R.14.2) with the match's intelligence (R.9). Computed in-memory;
    /// not fed into ranking/recommendation. The component fields explain the score
    /// deterministically.
    /// </summary>
    public sealed class MatchAffinityDto
    {
        public int UserId { get; init; }
        public int MatchId { get; init; }

        /// <summary>0-100 composite affinity.</summary>
        public int AffinityScore { get; init; }

        /// <summary>Banded level (Low/Medium/High/Critical) as a string.</summary>
        public string AffinityLevel { get; init; } = string.Empty;

        // ── Breakdown (each 0-100) ────────────────────────────────────────────
        public int TeamComponent { get; init; }
        public int LeagueComponent { get; init; }
        public int SignalComponent { get; init; }
        public int ImportanceComponent { get; init; }
    }
}
