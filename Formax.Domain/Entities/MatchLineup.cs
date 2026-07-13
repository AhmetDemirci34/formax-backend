namespace Formax.Domain.Entities
{
    /// <summary>
    /// Tracks whether official lineups have been released for a match.
    /// One row per match — upserted when the ingestion job fetches lineup data.
    /// </summary>
    public class MatchLineup
    {
        /// <summary>Primary key — same as Match.Id.</summary>
        public int MatchId { get; set; }

        public bool HomeLineupsReleased { get; set; }
        public bool AwayLineupsReleased { get; set; }

        /// <summary>UTC time when the lineup was first detected as released.</summary>
        public DateTime? ReleasedAt { get; set; }

        /// <summary>UTC time of the most recent fetch from the provider.</summary>
        public DateTime FetchedAt { get; set; }

        // Navigation
        public Match? Match { get; set; }
    }
}
