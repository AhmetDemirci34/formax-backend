using System;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.1) — the assembled facts about a single match that
    /// the signal rules read. Built from match + team data; the first stage of the
    /// MatchProfile → MatchSignal → MatchIntelligenceSnapshot pipeline.
    /// </summary>
    public sealed class MatchProfile
    {
        public int MatchId { get; init; }

        public int HomeTeamId { get; init; }
        public int AwayTeamId { get; init; }

        public string HomeTeamName { get; init; } = string.Empty;
        public string AwayTeamName { get; init; } = string.Empty;

        public string League { get; init; } = string.Empty;
        public DateTime MatchDate { get; init; }

        public int? HomeRank { get; init; }
        public int? AwayRank { get; init; }

        public bool HomeIsStable { get; init; }
        public bool AwayIsStable { get; init; }
    }
}
