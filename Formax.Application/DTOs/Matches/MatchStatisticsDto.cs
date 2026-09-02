using System.Collections.Generic;
using Formax.Domain.Entities;

namespace Formax.Application.DTOs.Matches
{
    /// <summary>Tek istatistik satırı — ev/deplasman karşılaştırması.</summary>
    public sealed class MatchStatisticRowDto
    {
        public string Key { get; init; } = string.Empty;
        /// <summary>Türkçe etiket ("Topa sahip olma").</summary>
        public string Label { get; init; } = string.Empty;
        public int Home { get; init; }
        public int Away { get; init; }
        /// <summary>Yüzde biriminde mi? (topa sahip olma). Ekran "%" ekler.</summary>
        public bool IsPercentage { get; init; }
    }

    /// <summary>
    /// MAÇ İSTATİSTİKLERİ — YALNIZ GERÇEK VERİ.
    ///
    /// KRİTİK KURAL (ölçüldü 02.09.2026): depoda maç için bir istatistik SATIRI olması,
    /// istatistik VERİSİ olduğu anlamına gelmez. Fenerbahçe–Lyon'un iki ayağında da
    /// <c>MatchLiveStats</c> satırı vardır ama skor dışındaki BÜTÜN alanlar sıfırdır
    /// (canlı alım kapalı olduğu için hiç doldurulmamış). Bu satırı ekrana basmak
    /// kullanıcıya "%0 topa sahip olma, 0 şut" diye YANLIŞ bir maç anlatır.
    ///
    /// Bu yüzden <see cref="From"/> yalnız EN AZ BİR alanı sıfırdan farklıysa DTO döner;
    /// aksi hâlde null döner ve ekran istatistik bölümünü hiç render etmez.
    ///
    /// Tahmin motorundan istatistik TÜRETİLMEZ.
    /// </summary>
    public sealed class MatchStatisticsDto
    {
        public List<MatchStatisticRowDto> Rows { get; init; } = new();

        public static MatchStatisticsDto? From(MatchLiveStats? s)
        {
            if (s == null) return null;

            var rows = new List<MatchStatisticRowDto>
            {
                Row("possession",   "Topa sahip olma", s.PossessionHome,      s.PossessionAway,      percentage: true),
                Row("shots",        "Toplam şut",      s.ShotsHome,           s.ShotsAway),
                Row("shotsOnTarget","İsabetli şut",    s.ShotsOnTargetHome,   s.ShotsOnTargetAway),
                Row("corners",      "Korner",          s.CornersHome,         s.CornersAway),
                Row("fouls",        "Faul",            s.FoulsHome,           s.FoulsAway),
                Row("offsides",     "Ofsayt",          s.OffsidesHome,        s.OffsidesAway),
                Row("yellow",       "Sarı kart",       s.YellowHome,          s.YellowAway),
                Row("red",          "Kırmızı kart",    s.RedHome,             s.RedAway)
            };

            // TAMAMI SIFIR = VERİ YOK. Boş bir satır kümesi "0-0 istatistik" değildir.
            var hasAnyValue = false;
            foreach (var r in rows)
                if (r.Home != 0 || r.Away != 0) { hasAnyValue = true; break; }
            if (!hasAnyValue) return null;

            return new MatchStatisticsDto { Rows = rows };
        }

        private static MatchStatisticRowDto Row(
            string key, string label, int home, int away, bool percentage = false)
            => new() { Key = key, Label = label, Home = home, Away = away, IsPercentage = percentage };
    }
}
