using System.Collections.Generic;
using Formax.Domain.Constants;

namespace Formax.Application.Services.Odds
{
    /// <summary>
    /// FORMAX AI market adı → normalize oran anahtarı köprüsü.
    ///
    /// Kaynak taraf: <c>ProbabilityEngine</c>'in ürettiği market adları (Decision Package).
    /// Hedef taraf: <see cref="OddsMarketKeys"/> (sağlayıcıdan gelen gerçek oranların anahtarı).
    ///
    /// Karşılığı OLMAYAN marketler (ör. "Toplam Gol: ...", "Skor Aralığı: ...") kasten
    /// eşlenmez — bu marketlerde oran gösterilmez, benzer bir markete YAKLAŞTIRILMAZ.
    /// </summary>
    public static class DecisionMarketOddsMapper
    {
        private static readonly Dictionary<string, string> Map = new()
        {
            ["Ev Sahibi Kazanır"]         = OddsMarketKeys.Ms1,
            ["Beraberlik"]                = OddsMarketKeys.MsX,
            ["Deplasman Kazanır"]         = OddsMarketKeys.Ms2,

            ["Çifte Şans (1X)"]           = OddsMarketKeys.DoubleChance1X,
            ["Çifte Şans (X2)"]           = OddsMarketKeys.DoubleChanceX2,
            ["Çifte Şans (1-2)"]          = OddsMarketKeys.DoubleChance12,

            ["2.5 Üst"]                   = OddsMarketKeys.Over25,
            ["2.5 Alt"]                   = OddsMarketKeys.Under25,

            ["Karşılıklı Gol Var"]        = OddsMarketKeys.BttsYes,
            ["Karşılıklı Gol Yok"]        = OddsMarketKeys.BttsNo,
            ["KG Var"]                    = OddsMarketKeys.BttsYes,
            ["KG Yok"]                    = OddsMarketKeys.BttsNo,

            ["İlk Yarı Ev Sahibi Önde"]   = OddsMarketKeys.Ht1,
            ["İlk Yarı Beraberlik"]       = OddsMarketKeys.HtX,
            ["İlk Yarı Deplasman Önde"]   = OddsMarketKeys.Ht2,

            ["İlk Gol Ev Sahibi"]         = OddsMarketKeys.FirstGoalHome,
            ["İlk Gol Deplasman"]         = OddsMarketKeys.FirstGoalAway,
        };

        /// <summary>Market adının oran anahtarı; eşleşme yoksa null.</summary>
        public static string? ToOddsKey(string? market)
        {
            if (string.IsNullOrWhiteSpace(market)) return null;
            return Map.TryGetValue(market.Trim(), out var key) ? key : null;
        }
    }
}
