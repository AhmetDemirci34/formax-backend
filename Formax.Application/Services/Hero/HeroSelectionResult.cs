using Formax.Application.Services.Players.Intelligence;

namespace Formax.Application.Services.Hero
{
    /// <summary>
    /// HeroSelectionEngine çıktısı (Domain Model — DTO değil). HeroAggregate tüketir.
    /// Engine yalnız SEÇİM yapar: IntelligenceScore okur, Hero'ları ve HeroReasonType'ı
    /// belirler, mevcut sinyallerden HeroConfidence türetir. Narrative burada ÜRETİLMEZ.
    /// GeneratedAt yok — zaman bilgisini cache katmanı yönetir.
    /// </summary>
    public sealed class HeroSelectionResult
    {
        /// <summary>Ev sahibinden en yüksek IntelligenceScore'lu oyuncu. Kadro yoksa null.</summary>
        public PlayerIntelligence? HomeHero { get; set; }

        /// <summary>Deplasmandan en yüksek IntelligenceScore'lu oyuncu. Kadro yoksa null.</summary>
        public PlayerIntelligence? AwayHero { get; set; }

        /// <summary>Baskın Hero'nun (yüksek skorlu) seçim gerekçe tipi.</summary>
        public HeroReasonType HeroReasonType { get; set; }

        /// <summary>Mevcut sinyallerden (hero skor + Radar Confidence + Importance) türetilen güven (0–100).</summary>
        public int HeroConfidence { get; set; }
    }
}
