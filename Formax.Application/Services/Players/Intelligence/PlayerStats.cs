namespace Formax.Application.Services.Players.Intelligence
{
    /// <summary>
    /// Bir oyuncunun ham istatistikleri (IPlayerStatsProvider çıktısı). Tüm alanlar
    /// nullable — sağlayıcı bulamazsa Found=false ve alanlar boş kalır (Graceful Fallback).
    /// </summary>
    public sealed class PlayerStats
    {
        public string PlayerName { get; set; } = "";
        public int? ExternalPlayerId { get; set; }

        public double? Rating { get; set; }       // sezon ortalama rating (0–10)
        public double? FormRating { get; set; }    // son maçlar ortalama rating (0–10)
        public int? Goals { get; set; }
        public int? Assists { get; set; }
        public int? Minutes { get; set; }
        public int? AppearanceCount { get; set; }

        /// <summary>Sağlayıcı bu oyuncuyu bulabildi mi.</summary>
        public bool Found { get; set; }
    }
}
