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

        // ── KİMLİK VE KATILIM (19.09.2026 · additive, hepsi null olabilir) ──────────
        // Geçmiş kadro backfill'i oyuncu kimliğini METİNDEN kurmaz: resmî kaynağın kendi
        // oyuncu kimliği burada saklanır ve eşleme önce bunun üzerinden yapılır.

        /// <summary>
        /// Resmî kaynağın oyuncu kimliği (ör. "154561", "serie-a::Football_Player::…", "tff:12345").
        /// Kaynak vermiyorsa null — UYDURULMAZ.
        /// </summary>
        public string? OfficialPlayerId { get; set; }

        /// <summary>
        /// Oyuncunun oyundan çıktığı ya da oyuna girdiği dakika — YALNIZ kaynak gerçekten
        /// yayımladığında. Kaynak değişiklik verisi vermiyorsa null kalır (90 yazılmaz).
        /// </summary>
        public int? SubstitutionMinute { get; set; }

        /// <summary>
        /// Sahada geçirilen dakika — yalnız kaynağın gerçek değişiklik verisinden HESAPLANABİLDİĞİNDE.
        /// Değişiklik verisi olmayan kaynakta (ör. TFF) null kalır; ilk 11 oynadı diye 90 YAZILMAZ.
        /// </summary>
        public int? MinutesPlayed { get; set; }
    }
}
