namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.2) — head-to-head summary between the two teams,
    /// expressed from the current match's home/away perspective.
    /// </summary>
    public sealed class H2HContext
    {
        public int Meetings { get; init; }

        public int HomeWins { get; init; }
        public int AwayWins { get; init; }
        public int Draws { get; init; }

        public int TotalGoals { get; init; }
        public double AvgGoals { get; init; }

        /// <summary>Short summary of the most recent meeting, e.g. "Galatasaray 2-1 Fenerbahçe".</summary>
        public string LastMeetingSummary { get; init; } = string.Empty;

        public bool HasData => Meetings > 0;
    }
}
