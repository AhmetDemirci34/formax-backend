namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX GDP — api-football <c>/predictions?fixture=</c> öngörüsünün canonical kaydı.
    /// YALNIZ AI sinyali; kullanıcıya ASLA gösterilmez (hiçbir kullanıcı-yüzeyi DTO'suna bağlanmaz).
    ///
    /// Identity: api-football fixture id (<see cref="Match.ExternalMatchId"/>) → canonical
    /// <see cref="Match.Id"/>. PK = MatchId (maç-başına tek satır; CompetitionContext deseni).
    /// Coverage yoksa satır yazılmaz → tüketici HasData=false (fake YOK).
    ///
    /// Günde bir WorldPerceptionDailyJob (04:00 UTC) tarafından yenilenir.
    /// </summary>
    public class MatchPredictionSignal
    {
        /// <summary>Internal Match.Id — PK.</summary>
        public int MatchId { get; set; }

        /// <summary>Provider fixture id (iz sürme için).</summary>
        public string ExternalMatchId { get; set; } = string.Empty;

        // ── Provider olasılıkları (0-100) ────────────────────────────────────────
        public int PercentHome { get; set; }
        public int PercentDraw { get; set; }
        public int PercentAway { get; set; }

        /// <summary>Öngörülen kazanan takım adı (boş olabilir).</summary>
        public string WinnerName { get; set; } = string.Empty;

        /// <summary>Kazanan taraf: "Home" | "Away" | "" (beraberlik/öngörü yok).</summary>
        public string WinnerSide { get; set; } = string.Empty;

        public bool WinOrDraw { get; set; }

        /// <summary>Provider tavsiyesi (ProviderRecommendation kaynağı).</summary>
        public string Advice { get; set; } = string.Empty;

        /// <summary>Üst/Alt öngörüsü (ör. "-3.5").</summary>
        public string UnderOver { get; set; } = string.Empty;

        /// <summary>Karşılaştırma toplam güç yüzdeleri (ProviderTrend kaynağı).</summary>
        public int ComparisonTotalHome { get; set; }
        public int ComparisonTotalAway { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
