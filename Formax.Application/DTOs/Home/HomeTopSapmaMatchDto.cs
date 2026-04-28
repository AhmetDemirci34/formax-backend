namespace Formax.Application.DTOs.Home
{
    /// <summary>
    /// FORMAX v1.4 — FAZ 4.2 (KİLİTLİ)
    /// Home TopSapma vitrini için sadeleştirilmiş maç DTO'su.
    ///
    /// Not: Bu endpoint "ölçüm" içindir. Oran/yüzde/tahmin dili yok.
    /// </summary>
    public sealed class HomeTopSapmaMatchDto
    {
        public int MatchId { get; init; }

        public HomeTeamsDto Teams { get; init; } = new();

        public int? Sapma { get; init; }

        /// <summary>
        /// Sapma bölgesi (Denge / Dikkat Çekici / Yanılma Riski / Yüksek Sapma)
        /// </summary>
        public string? Bolge { get; init; }

        /// <summary>
        /// Freshness (Live / LastKnown / Stale)
        /// </summary>
        public string? Freshness { get; init; }

        public bool? SessizMi { get; init; }
    }
}
