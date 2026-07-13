namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.2) — recent form for one team, computed from its
    /// last finished matches. Part of the enriched MatchContext.
    /// </summary>
    public sealed class TeamFormContext
    {
        public int TeamId { get; init; }
        public string TeamName { get; init; } = string.Empty;

        public int Played { get; init; }
        public int Wins { get; init; }
        public int Draws { get; init; }
        public int Losses { get; init; }

        public int GoalsFor { get; init; }
        public int GoalsAgainst { get; init; }

        /// <summary>Recent-to-old result string, e.g. "WWDLW".</summary>
        public string FormString { get; init; } = string.Empty;

        public int Points { get; init; }

        /// <summary>Normalized 0..100 form score (Points / maxPoints × 100).</summary>
        public double FormScore { get; init; }

        public bool HasData => Played > 0;
    }
}
