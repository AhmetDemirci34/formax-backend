using System;
using Formax.Domain.Constants;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Picks
{
    /// <summary>
    /// SEÇİM SONUCU — yalnız DESTEKLENEN marketlerde hesaplanır.
    ///
    /// KURAL: hesaplanamayan seçim için sonuç UYDURULMAZ. Bir marketin doğru/yanlış
    /// kararı ancak maçın kesin sonucundan TÜRETİLEBİLİYORSA verilir. İlk yarı
    /// marketleri yalnız İY skoru depoda varsa; "ilk golü kim attı" ise ancak olay
    /// kaydı varsa. Aksi hâlde <see cref="PickSettlementOutcome.Unsettleable"/> döner
    /// ve arayüz "hesaplanamadı" der — rastgele bir doğru/yanlış GÖSTERMEZ.
    ///
    /// Saf fonksiyondur: veritabanı, saat ve ağ okumaz.
    /// </summary>
    public static class PickSettlement
    {
        /// <summary>Bir seçimin sonucu.</summary>
        public enum PickSettlementOutcome
        {
            /// <summary>Bu market için sonuç hesaplanamıyor — uydurma YOK.</summary>
            Unsettleable = 0,
            Won = 1,
            Lost = 2
        }

        /// <summary>Maçın kesinleşmiş sonucu — hesaplamanın tek girdisi.</summary>
        public readonly record struct FinalScore(
            int HomeGoals,
            int AwayGoals,
            int? HalfTimeHomeGoals,
            int? HalfTimeAwayGoals);

        /// <summary>
        /// Bu market anahtarının sonucu hesaplanabilir mi?
        /// Arayüz "Tamamlandı" mı yoksa "hesaplanamadı" mı yazacağını buradan bilir.
        /// </summary>
        public static bool IsSupported(string? marketKey, FinalScore score) =>
            marketKey switch
            {
                OddsMarketKeys.Ms1 or OddsMarketKeys.MsX or OddsMarketKeys.Ms2 => true,
                OddsMarketKeys.DoubleChance1X or OddsMarketKeys.DoubleChanceX2
                    or OddsMarketKeys.DoubleChance12 => true,
                OddsMarketKeys.Over25 or OddsMarketKeys.Under25 => true,
                OddsMarketKeys.Over15 or OddsMarketKeys.Under15 or OddsMarketKeys.Over35 or OddsMarketKeys.Under35 => true,
                OddsMarketKeys.BttsYes or OddsMarketKeys.BttsNo => true,

                // İLK YARI: yalnız İY skoru GERÇEKTEN varsa. Depoda null iken 0-0
                // varsayıp hesaplamak, olmayan veriden sonuç üretmek olurdu.
                OddsMarketKeys.Ht1 or OddsMarketKeys.HtX or OddsMarketKeys.Ht2
                    or OddsMarketKeys.HtOver05
                    => score.HalfTimeHomeGoals.HasValue && score.HalfTimeAwayGoals.HasValue,

                // İLK GOL: skordan TÜRETİLEMEZ (olay sırası gerekir). Olay kaydı
                // hattı yerleştiğinde burada desteklenebilir; şimdilik hesaplanmaz.
                _ => false
            };

        /// <summary>Seçimin sonucu. Desteklenmeyen markette <c>Unsettleable</c>.</summary>
        public static PickSettlementOutcome Settle(string? marketKey, FinalScore s)
        {
            if (!IsSupported(marketKey, s)) return PickSettlementOutcome.Unsettleable;

            var total = s.HomeGoals + s.AwayGoals;
            var homeWon = s.HomeGoals > s.AwayGoals;
            var awayWon = s.AwayGoals > s.HomeGoals;
            var draw = s.HomeGoals == s.AwayGoals;

            bool hit = marketKey switch
            {
                OddsMarketKeys.Ms1 => homeWon,
                OddsMarketKeys.MsX => draw,
                OddsMarketKeys.Ms2 => awayWon,

                OddsMarketKeys.DoubleChance1X => homeWon || draw,
                OddsMarketKeys.DoubleChanceX2 => awayWon || draw,
                OddsMarketKeys.DoubleChance12 => homeWon || awayWon,

                OddsMarketKeys.Over25  => total > 2,   // 2,5 üst → 3 ve fazlası
                OddsMarketKeys.Under25 => total < 3,   // 2,5 alt → 2 ve azı
                OddsMarketKeys.Over15  => total > 1,
                OddsMarketKeys.Under15 => total < 2,
                OddsMarketKeys.Over35  => total > 3,
                OddsMarketKeys.Under35 => total < 4,

                OddsMarketKeys.BttsYes => s.HomeGoals > 0 && s.AwayGoals > 0,
                OddsMarketKeys.BttsNo  => s.HomeGoals == 0 || s.AwayGoals == 0,

                OddsMarketKeys.Ht1 => s.HalfTimeHomeGoals!.Value > s.HalfTimeAwayGoals!.Value,
                OddsMarketKeys.HtX => s.HalfTimeHomeGoals!.Value == s.HalfTimeAwayGoals!.Value,
                OddsMarketKeys.Ht2 => s.HalfTimeAwayGoals!.Value > s.HalfTimeHomeGoals!.Value,
                OddsMarketKeys.HtOver05 =>
                    s.HalfTimeHomeGoals!.Value + s.HalfTimeAwayGoals!.Value > 0,

                _ => false
            };

            return hit ? PickSettlementOutcome.Won : PickSettlementOutcome.Lost;
        }

        /// <summary>Sonucu mevcut <see cref="PickStatus"/> sözleşmesine çevirir.</summary>
        public static PickStatus ToStatus(PickSettlementOutcome outcome) => outcome switch
        {
            PickSettlementOutcome.Won => PickStatus.Win,
            PickSettlementOutcome.Lost => PickStatus.Lose,
            _ => PickStatus.Pending      // hesaplanamayan seçim "kaybetti" SAYILMAZ
        };
    }
}
