namespace Formax.Domain.Constants
{
    /// <summary>
    /// Normalize market anahtarları — sağlayıcı (api-football bet/value) ile FORMAX AI market
    /// adları arasındaki TEK köprü. Sağlayıcı tarafı Infrastructure'da, AI market adı tarafı
    /// Application'da bu anahtarlara eşlenir; iki taraf birbirini tanımaz.
    ///
    /// Anahtar seti kasten dardır: yalnız FORMAX'ın olasılık ürettiği ve sağlayıcıda birebir
    /// karşılığı olan marketler. Karşılığı olmayan market (ör. "Toplam Gol bandı", "Skor
    /// aralığı") eşlenmez — oran gösterilmez, uydurulmaz.
    /// </summary>
    public static class OddsMarketKeys
    {
        // Maç sonucu (1X2)
        public const string Ms1 = "MS1";
        public const string MsX = "MSX";
        public const string Ms2 = "MS2";

        // Çifte şans
        public const string DoubleChance1X = "CS_1X";
        public const string DoubleChance12 = "CS_12";
        public const string DoubleChanceX2 = "CS_X2";

        // Toplam gol 2.5
        public const string Over25  = "UST_2_5";
        public const string Under25 = "ALT_2_5";

        // Toplam gol 1.5 / 3.5 (15.09.2026, additive — olası sonuç motorunun gol ailesi)
        public const string Over15  = "UST_1_5";
        public const string Under15 = "ALT_1_5";
        public const string Over35  = "UST_3_5";
        public const string Under35 = "ALT_3_5";

        // Karşılıklı gol
        public const string BttsYes = "KG_VAR";
        public const string BttsNo  = "KG_YOK";

        // İlk yarı sonucu
        public const string Ht1 = "IY_1";
        public const string HtX = "IY_X";
        public const string Ht2 = "IY_2";

        // İlk yarı gol var (0.5 üst)
        public const string HtOver05 = "IY_UST_0_5";

        // İlk golü atan
        public const string FirstGoalHome = "ILK_GOL_EV";
        public const string FirstGoalAway = "ILK_GOL_DEP";
    }
}
