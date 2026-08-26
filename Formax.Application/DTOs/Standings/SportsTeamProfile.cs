namespace Formax.Application.DTOs.Standings
{
    /// <summary>
    /// Phase 6 Final — bir takımın api-football profil verisi: /coachs + /teams (venue) +
    /// /players/squads + /transfers birleşimi (hepsi takım-kapsamlı, düşük frekanslı).
    /// Her alt-blok bağımsız coverage-gated: veri yoksa ilgili Has* = false (fake YOK).
    /// YALNIZ AI/GDP sinyali; kullanıcı DTO'suna bağlanmaz.
    /// </summary>
    public sealed class SportsTeamProfile
    {
        // ── /coachs?team= ────────────────────────────────────────────────────────
        public bool HasCoach { get; set; }
        public string CoachName { get; set; } = string.Empty;
        public int CoachAge { get; set; }

        // ── /teams?id= (venue bloğu) ─────────────────────────────────────────────
        public bool HasVenue { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public string VenueCity { get; set; } = string.Empty;
        public int VenueCapacity { get; set; }
        public string VenueSurface { get; set; } = string.Empty;

        // ── /players/squads?team= ────────────────────────────────────────────────
        public bool HasSquad { get; set; }
        public int SquadSize { get; set; }
        public double SquadAvgAge { get; set; }

        // ── /transfers?team= (son 12 ay) ─────────────────────────────────────────
        public bool HasTransfers { get; set; }
        public int RecentTransfersIn { get; set; }
        public int RecentTransfersOut { get; set; }
    }
}
