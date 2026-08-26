namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX GDP — api-football takım profili (coach + venue + squad + transfers birleşimi).
    /// Takım-kapsamlı, düşük frekanslı; WorldPerceptionDailyJob (04:00) yeniler. AI/GDP-only.
    ///
    /// Identity: ingestion internal Team.Id → external id çözer, provider'ı external ile çağırır,
    /// kaydı INTERNAL <see cref="TeamId"/> (PK) ile saklar → builder doğrudan home/away Team.Id ile okur.
    /// Her alt-blok bağımsız coverage-gated (Has* flag). Coverage yoksa satır yazılmaz (fake YOK).
    /// </summary>
    public class TeamProfileSignal
    {
        /// <summary>Internal Team.Id — PK.</summary>
        public int TeamId { get; set; }

        /// <summary>Provider external takım id'si (iz sürme).</summary>
        public string ExternalTeamId { get; set; } = string.Empty;

        // ── Coach ────────────────────────────────────────────────────────────────
        public bool HasCoach { get; set; }
        public string CoachName { get; set; } = string.Empty;
        public int CoachAge { get; set; }

        // ── Venue ────────────────────────────────────────────────────────────────
        public bool HasVenue { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public string VenueCity { get; set; } = string.Empty;
        public int VenueCapacity { get; set; }
        public string VenueSurface { get; set; } = string.Empty;

        // ── Squad ────────────────────────────────────────────────────────────────
        public bool HasSquad { get; set; }
        public int SquadSize { get; set; }
        public double SquadAvgAge { get; set; }

        // ── Transfers (son 12 ay) ────────────────────────────────────────────────
        public bool HasTransfers { get; set; }
        public int RecentTransfersIn { get; set; }
        public int RecentTransfersOut { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
