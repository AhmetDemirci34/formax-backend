namespace Formax.Domain.Entities
{
    /// <summary>
    /// A single player entry in the official lineup for a match.
    /// </summary>
    public class MatchLineupPlayer
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int MatchId { get; set; }

        /// <summary>"Home" or "Away"</summary>
        public string Side { get; set; } = string.Empty;

        /// <summary>"Starter" or "Bench"</summary>
        public string Role { get; set; } = string.Empty;

        public int ShirtNumber { get; set; }
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>Position abbreviation: G, D, M, F</summary>
        public string Position { get; set; } = string.Empty;

        /// <summary>
        /// Sağlayıcının açıkladığı saha koordinatı "hat:sıra" (ör. "1:1", "2:4").
        /// Yedeklerde ve sağlayıcı vermediğinde null olur — üretilmez.
        /// </summary>
        public string? Grid { get; set; }

        public bool IsCaptain { get; set; }
    }
}
