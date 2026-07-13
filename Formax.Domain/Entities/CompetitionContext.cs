namespace Formax.Domain.Entities
{
    /// <summary>
    /// Competition context for a specific match.
    /// One row per match — keyed by MatchId.
    /// Refreshed daily for upcoming fixtures that have ExternalMatchId set.
    /// </summary>
    public class CompetitionContext
    {
        /// <summary>Primary key — same as Match.Id.</summary>
        public int MatchId { get; set; }

        /// <summary>"League" | "Cup" | "Knockout"</summary>
        public string CompetitionType { get; set; } = "League";

        /// <summary>Stage/round label, e.g. "Regular Season - 25" or "Quarter-Final".</summary>
        public string StageName { get; set; } = string.Empty;

        /// <summary>Short human-readable headline, e.g. "Premier League — Hafta 25".</summary>
        public string ContextHeadline { get; set; } = string.Empty;

        /// <summary>One-sentence summary describing competitive stakes.</summary>
        public string ContextSummary { get; set; } = string.Empty;

        /// <summary>JSON-encoded bracket tree — populated for Cup / Knockout types only.</summary>
        public string? BracketJson { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
