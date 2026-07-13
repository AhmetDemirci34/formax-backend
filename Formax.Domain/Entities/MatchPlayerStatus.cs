namespace Formax.Domain.Entities
{
    /// <summary>
    /// Injury, suspension, or doubtful status for a player associated with a match.
    /// Fetched from the sports data provider per-match window.
    /// </summary>
    public class MatchPlayerStatus
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int MatchId { get; set; }
        public int TeamId { get; set; }

        public string PlayerName { get; set; } = string.Empty;

        /// <summary>"Injured" | "Suspended" | "Doubtful"</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Free-text reason provided by the provider (e.g. "Knee Injury").</summary>
        public string Reason { get; set; } = string.Empty;

        public DateTime FetchedAt { get; set; }
    }
}
