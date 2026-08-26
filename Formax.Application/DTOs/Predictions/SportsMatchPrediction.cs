namespace Formax.Application.DTOs.Predictions
{
    /// <summary>
    /// api-football <c>/predictions?fixture=</c> sonucunun sağlayıcı-nötr taşıyıcısı.
    /// YALNIZ AI sinyali; kullanıcıya asla gösterilmez. Coverage yoksa provider null döner
    /// → ingestion satır yazmaz → tüketici HasData=false (fake YOK).
    /// </summary>
    public sealed class SportsMatchPrediction
    {
        /// <summary>Provider olasılık yüzdeleri (0-100).</summary>
        public int PercentHome { get; set; }
        public int PercentDraw { get; set; }
        public int PercentAway { get; set; }

        /// <summary>Provider'ın öngördüğü kazanan takım adı (boş olabilir).</summary>
        public string WinnerName { get; set; } = string.Empty;

        /// <summary>Kazanan taraf: "Home" | "Away" | "" (beraberlik/öngörü yok).</summary>
        public string WinnerSide { get; set; } = string.Empty;

        /// <summary>Provider "kazanır ya da berabere" işaretini verdi mi.</summary>
        public bool WinOrDraw { get; set; }

        /// <summary>Provider tavsiyesi (ör. "Combo Double chance ... and -3.5 goals").</summary>
        public string Advice { get; set; } = string.Empty;

        /// <summary>Üst/Alt öngörüsü (ör. "-3.5").</summary>
        public string UnderOver { get; set; } = string.Empty;

        /// <summary>Karşılaştırma toplam güç yüzdeleri (provider "comparison.total").</summary>
        public int ComparisonTotalHome { get; set; }
        public int ComparisonTotalAway { get; set; }
    }
}
