namespace Formax.Application.DTOs.Lineup
{
    // ─── Provider result models (returned by ISportsDataProvider) ─────────────

    /// <summary>Full lineup result from the sports data provider for one fixture.</summary>
    public class SportsLineupResult
    {
        public bool LineupsAnnounced { get; set; }

        public List<SportsLineupPlayer> HomeStarters { get; set; } = new();
        public List<SportsLineupPlayer> HomeBench { get; set; } = new();

        public List<SportsLineupPlayer> AwayStarters { get; set; } = new();
        public List<SportsLineupPlayer> AwayBench { get; set; } = new();
    }

    public class SportsLineupPlayer
    {
        public string Name { get; set; } = string.Empty;
        public int ShirtNumber { get; set; }

        /// <summary>Position abbreviation: G, D, M, F</summary>
        public string Position { get; set; } = string.Empty;

        public bool IsCaptain { get; set; }
    }

    /// <summary>Single player status returned by the sports data provider.</summary>
    public class SportsPlayerStatusResult
    {
        public string PlayerName { get; set; } = string.Empty;
        public int TeamId { get; set; }

        /// <summary>"Injured" | "Suspended" | "Doubtful"</summary>
        public string Status { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }

    // ─── MatchDetail DTO section models (consumed by the frontend) ────────────

    public class LineupPlayerDto
    {
        public int ShirtNumber { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public bool IsCaptain { get; set; }
    }

    public class PlayerStatusDto
    {
        public string PlayerName { get; set; } = string.Empty;
        public int TeamId { get; set; }

        /// <summary>"Injured" | "Suspended" | "Doubtful"</summary>
        public string Status { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }

    public class LineupSectionDto
    {
        /// <summary>
        /// True when at least one side has officially released their lineup.
        /// </summary>
        public bool LineupsAnnounced { get; set; }

        public List<LineupPlayerDto> HomeStartingXI { get; set; } = new();
        public List<LineupPlayerDto> AwayStartingXI { get; set; } = new();

        public List<LineupPlayerDto> HomeBench { get; set; } = new();
        public List<LineupPlayerDto> AwayBench { get; set; } = new();
    }

    public class PlayerStatusSectionDto
    {
        public List<PlayerStatusDto> Injuries { get; set; } = new();
        public List<PlayerStatusDto> Suspensions { get; set; } = new();
        public List<PlayerStatusDto> Doubtful { get; set; } = new();
    }
}
