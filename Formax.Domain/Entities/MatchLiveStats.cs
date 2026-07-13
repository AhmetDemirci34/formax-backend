namespace Formax.Domain.Entities
{
    /// <summary>
    /// Current live statistics snapshot for an active match.
    /// One row per match — upserted every 30 seconds by LiveMatchIngestionJob.
    /// Persisted for finished matches as the final stat record.
    /// </summary>
    public class MatchLiveStats
    {
        /// <summary>Primary key — same as Match.Id.</summary>
        public int MatchId { get; set; }

        // ── Score & clock ──────────────────────────────────────────────────────
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }

        /// <summary>Current match minute (null if not started or provider unavailable).</summary>
        public int? Minute { get; set; }

        /// <summary>Match phase code: "1H" | "HT" | "2H" | "ET" | "P" | "FT".</summary>
        public string Phase { get; set; } = string.Empty;

        // ── Possession (0-100) ─────────────────────────────────────────────────
        public int PossessionHome { get; set; }
        public int PossessionAway { get; set; }

        // ── Shots ──────────────────────────────────────────────────────────────
        public int ShotsHome { get; set; }
        public int ShotsAway { get; set; }
        public int ShotsOnTargetHome { get; set; }
        public int ShotsOnTargetAway { get; set; }

        // ── Set pieces & discipline ────────────────────────────────────────────
        public int CornersHome { get; set; }
        public int CornersAway { get; set; }
        public int FoulsHome { get; set; }
        public int FoulsAway { get; set; }
        public int OffsidesHome { get; set; }
        public int OffsidesAway { get; set; }
        public int YellowHome { get; set; }
        public int YellowAway { get; set; }
        public int RedHome { get; set; }
        public int RedAway { get; set; }

        // ── Attacks ────────────────────────────────────────────────────────────
        public int DangerousAttacksHome { get; set; }
        public int DangerousAttacksAway { get; set; }

        // ── xG (nullable — provider may not supply) ────────────────────────────
        public double? XgHome { get; set; }
        public double? XgAway { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
