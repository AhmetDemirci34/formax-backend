using System;
using System.Collections.Generic;
using Formax.Domain.Constants;

namespace Formax.Application.Services.Picks
{
    /// <summary>
    /// MARKET GRUBU — "bu iki seçim aynı anda doğru olabilir mi?" sorusunun TEK yeri.
    ///
    /// Kullanıcı birden fazla olası sonuç seçebilir; ama aynı grubun iki seçeneği
    /// birbirini DIŞLAR. "Karşılıklı Gol Var" ile "Karşılıklı Gol Yok" aynı maçta
    /// birlikte gerçekleşemez; "2.5 Alt" ile "2.5 Üst" de öyle.
    ///
    /// GRUP, MARKET AİLESİNDEN DAHA DARDIR. <c>AiProbability.Family</c> "Totals" der
    /// ve 2.5 ile 3.5 çizgilerini AYNI kovaya koyar — oysa "2.5 Alt" ile "3.5 Üst"
    /// birlikte seçilebilir (2.5 altı bir maç 3.5 üstü olamaz; ama örnek gösterir ki
    /// çizgi bilgisi olmadan karar verilemez). Bu yüzden grup ÇİZGİ düzeyindedir.
    ///
    /// KURAL BURADA DURUR ki iki yüzey (seçim ekranı ve kayıt ucu) ayrışamasın:
    /// arayüz aynı grubu görsel olarak değiştirirken backend de aynı grubu siler.
    /// </summary>
    public static class PickMarketGroups
    {
        /// <summary>Maç sonucu — 1 / X / 2 ve çifte şanslar aynı gruptadır.</summary>
        public const string MatchResult = "MATCH_RESULT";

        /// <summary>2,5 gol çizgisi — alt/üst.</summary>
        public const string TotalGoals25 = "TOTAL_2_5";

        /// <summary>Karşılıklı gol — var/yok.</summary>
        public const string BothTeamsToScore = "BTTS";

        /// <summary>İlk yarı sonucu — 1 / X / 2.</summary>
        public const string HalfTimeResult = "HT_RESULT";

        /// <summary>İlk golü kim atar — ev / deplasman.</summary>
        public const string FirstGoal = "FIRST_GOAL";

        /// <summary>
        /// Normalize oran anahtarı → grup. Anahtarı bilinmeyen market GRUPSUZDUR:
        /// hiçbir şeyi dışlamaz ve hiçbir şey tarafından dışlanmaz. Bilinmeyeni
        /// tahmini bir gruba koymak, kullanıcının geçerli bir seçimini SESSİZCE
        /// silmek olurdu.
        /// </summary>
        private static readonly Dictionary<string, string> ByMarketKey = new(StringComparer.Ordinal)
        {
            [OddsMarketKeys.Ms1]            = MatchResult,
            [OddsMarketKeys.MsX]            = MatchResult,
            [OddsMarketKeys.Ms2]            = MatchResult,
            // ÇİFTE ŞANS MAÇ SONUCU GRUBUNDADIR (ürün sözleşmesi): 1X ile X2 birlikte
            // seçilirse kullanıcı fiilen "her sonuç" demiş olur; bu bir tercih değildir.
            [OddsMarketKeys.DoubleChance1X] = MatchResult,
            [OddsMarketKeys.DoubleChanceX2] = MatchResult,
            [OddsMarketKeys.DoubleChance12] = MatchResult,

            [OddsMarketKeys.Over25]  = TotalGoals25,
            [OddsMarketKeys.Under25] = TotalGoals25,

            [OddsMarketKeys.BttsYes] = BothTeamsToScore,
            [OddsMarketKeys.BttsNo]  = BothTeamsToScore,

            [OddsMarketKeys.Ht1] = HalfTimeResult,
            [OddsMarketKeys.HtX] = HalfTimeResult,
            [OddsMarketKeys.Ht2] = HalfTimeResult,

            [OddsMarketKeys.FirstGoalHome] = FirstGoal,
            [OddsMarketKeys.FirstGoalAway] = FirstGoal
        };

        /// <summary>Normalize market anahtarının grubu; bilinmiyorsa null (grupsuz).</summary>
        public static string? GroupOf(string? marketKey)
            => !string.IsNullOrWhiteSpace(marketKey)
               && ByMarketKey.TryGetValue(marketKey.Trim(), out var g) ? g : null;

        /// <summary>
        /// İki seçim çelişir mi? Grubu aynı VE anahtarı farklı olan iki seçim çelişir.
        /// Grupsuz seçimler hiçbir zaman çelişmez.
        /// </summary>
        public static bool Conflicts(string? marketKeyA, string? marketKeyB)
        {
            if (string.IsNullOrWhiteSpace(marketKeyA) || string.IsNullOrWhiteSpace(marketKeyB))
                return false;
            if (string.Equals(marketKeyA, marketKeyB, StringComparison.Ordinal)) return false;

            var a = GroupOf(marketKeyA);
            var b = GroupOf(marketKeyB);
            return a != null && b != null && string.Equals(a, b, StringComparison.Ordinal);
        }
    }
}
