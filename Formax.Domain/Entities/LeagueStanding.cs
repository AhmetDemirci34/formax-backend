namespace Formax.Domain.Entities
{
    /// <summary>
    /// Standings row for a single team in a league season.
    /// Composite PK: (LeagueId, SeasonYear, TeamId).
    /// Refreshed daily at 04:00 UTC by WorldPerceptionDailyJob.
    /// </summary>
    public class LeagueStanding
    {
        /// <summary>Internal league ID — matches Match.LeagueId.</summary>
        public int LeagueId { get; set; }

        /// <summary>Season start year (e.g. 2024 for the 2024-25 season).</summary>
        public int SeasonYear { get; set; }

        /// <summary>Internal team ID — matches Team.Id.</summary>
        public int TeamId { get; set; }

        public string TeamName { get; set; } = string.Empty;

        public int Position { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDifference => GoalsFor - GoalsAgainst;
        public int Points { get; set; }

        /// <summary>Last 5 results string, e.g. "WWDLW".</summary>
        public string Form { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; }
    }
}
