using System;

namespace Formax.Application.Services.Players.Intelligence
{
    /// <summary>
    /// FORMAX Player Intelligence Engine çıktısı — bir oyuncunun tüm oyuncu-düzeyi zekâsı.
    ///
    /// Radar'ın YERİNE geçmez; yalnız oyuncu bazında üretir. Hero, Player Profile,
    /// Comparison ve Squad ekranları bu modeli ORTAK kullanır. Tüm skorlamayı
    /// (IntelligenceScore) ve gerekçeleri (PrimaryReason/SecondaryReason) bu engine üretir;
    /// HeroSelectionEngine yalnız OKUR.
    /// </summary>
    public sealed class PlayerIntelligence
    {
        // ── Kimlik (v1: PlayerName + TeamId; ExternalPlayerId ileride api-football için) ──
        public string PlayerName { get; set; } = "";
        public int TeamId { get; set; }
        public string Side { get; set; } = "";          // "Home" | "Away"
        public int? ExternalPlayerId { get; set; }      // nullable — bugün boş olabilir

        // ── Sinyaller (13) — her biri Value/Confidence/Available ──
        public PlayerSignal Form { get; set; } = PlayerSignal.Missing();
        public PlayerSignal Rating { get; set; } = PlayerSignal.Missing();
        public PlayerSignal Goals { get; set; } = PlayerSignal.Missing();
        public PlayerSignal Assists { get; set; } = PlayerSignal.Missing();
        public PlayerSignal Minutes { get; set; } = PlayerSignal.Missing();
        public PlayerSignal Availability { get; set; } = PlayerSignal.Missing();
        public PlayerSignal Injury { get; set; } = PlayerSignal.Missing();
        public PlayerSignal NewsImpact { get; set; } = PlayerSignal.Missing();
        public PlayerSignal Trend { get; set; } = PlayerSignal.Missing();
        public PlayerSignal Popularity { get; set; } = PlayerSignal.Missing();
        public PlayerSignal ExpectedImpact { get; set; } = PlayerSignal.Missing();
        public PlayerSignal Confidence { get; set; } = PlayerSignal.Missing();
        public PlayerSignal RecentPerformance { get; set; } = PlayerSignal.Missing();

        // ── Engine tarafından üretilen ortak çıktı (Hero/Profile/Comparison/Squad) ──
        /// <summary>Tüm mevcut sinyallerden hesaplanan bileşik zekâ skoru (0–100).</summary>
        public int IntelligenceScore { get; set; }

        /// <summary>En baskın katkı veren sinyalin insan-okur gerekçesi (ör. "Most Talked").</summary>
        public string PrimaryReason { get; set; } = "";

        /// <summary>İkincil gerekçe.</summary>
        public string SecondaryReason { get; set; } = "";

        /// <summary>Cache yönetimi için son güncelleme zamanı.</summary>
        public DateTime LastUpdated { get; set; }
    }
}
