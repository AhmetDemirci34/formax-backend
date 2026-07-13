namespace Formax.Application.DTOs.Standings
{
    // ─── Provider result models (returned by ISportsDataProvider) ─────────────

    public class SportsStandingEntry
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int Position { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int Points { get; set; }

        /// <summary>Last 5 results string, e.g. "WWDLW".</summary>
        public string Form { get; set; } = string.Empty;
    }

    public class SportsCompetitionContext
    {
        /// <summary>"League" | "Cup" | "Knockout"</summary>
        public string CompetitionType { get; set; } = "League";

        /// <summary>Stage/round label, e.g. "Regular Season - 25" or "Quarter-Final".</summary>
        public string StageName { get; set; } = string.Empty;

        /// <summary>League/competition name from the provider.</summary>
        public string LeagueName { get; set; } = string.Empty;
    }

    // ─── MatchDetail DTO section models (consumed by the frontend) ────────────

    /// <summary>Compact standing entry for a single team — used in the match detail response.</summary>
    public class TeamStandingDto
    {
        public int Position { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDifference { get; set; }
        public int Points { get; set; }
        public string Form { get; set; } = string.Empty;
        public bool IsHighlighted { get; set; }   // true for home/away team rows
    }

    public class StandingSectionDto
    {
        public int LeagueId { get; set; }
        public int SeasonYear { get; set; }

        /// <summary>Full standing entry for the home team — null if not found.</summary>
        public TeamStandingDto? HomeTeamPeek { get; set; }

        /// <summary>Full standing entry for the away team — null if not found.</summary>
        public TeamStandingDto? AwayTeamPeek { get; set; }

        /// <summary>
        /// Narrow slice of the table — positions relevant to this fixture
        /// (top 3 + home/away positions ± 2, de-duped, max 10 rows).
        /// </summary>
        public List<TeamStandingDto> TableSlice { get; set; } = new();
    }

    public class CompetitionContextSectionDto
    {
        /// <summary>"League" | "Cup" | "Knockout"</summary>
        public string CompetitionType { get; set; } = "League";

        public string StageName { get; set; } = string.Empty;
        public string ContextHeadline { get; set; } = string.Empty;
        public string ContextSummary { get; set; } = string.Empty;

        /// <summary>Null unless CompetitionType is Cup or Knockout.</summary>
        public string? BracketJson { get; set; }
    }
}
