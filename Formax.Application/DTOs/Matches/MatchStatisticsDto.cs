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

        /// <summary>
        /// KAYNAKTA YAYIMLANMAYAN ALANLAR — resmî kaynak bu ölçümü vermedi (null). Ekran bunları "0" diye GÖSTERMEZ;
        /// yalnız "kaynakta yayımlanmadı" diye listeler. Gerçek 0 ise <see cref="Rows"/> içinde 0 olarak durur.
        /// </summary>
        public List<string> NotPublished { get; init; } = new();

        /// <summary>İstatistiğin resmî kaynağı ("LALIGA" …); eski/lisanslı kayıtta null.</summary>
        public string? SourceName { get; init; }

        /// <summary>
        /// KANONİK MAÇ SONRASI İSTATİSTİKLERİ — bitmiş maç ekranının ÖNCELİKLİ kaynağı.
        ///
        /// <see cref="MatchTeamStatistic"/> nullable'dır: sağlayıcının göndermediği ölçüm
        /// null kalır ve satır HİÇ ÜRETİLMEZ. Böylece "0 korner" ile "korner bilgisi yok"
        /// ekranda ayrışır — eski canlı tabloda ikisi de 0 görünüyordu (ölçüldü 06.09.2026:
        /// 87.546 satırın 87.502'si skor dışında tamamen sıfırdı).
        ///
        /// İki taraftan biri eksikse o ölçüm gösterilmez: tek taraflı istatistik
        /// karşılaştırma satırı kuramaz.
        /// </summary>
        public static MatchStatisticsDto? FromTeamRows(
            MatchTeamStatistic? home, MatchTeamStatistic? away)
        {
            if (home == null || away == null) return null;

            var rows = new List<MatchStatisticRowDto>();

            var notPublished = new List<string>();
            void Add(string key, string label, int? h, int? a, bool percentage = false)
            {
                if (h.HasValue && a.HasValue)
                    rows.Add(Row(key, label, h.Value, a.Value, percentage));
                else
                    notPublished.Add(label);
            }

            Add("possession",    "Topa sahip olma",  home.BallPossession,  away.BallPossession, percentage: true);
            Add("shots",         "Toplam şut",       home.TotalShots,      away.TotalShots);
            Add("shotsOnTarget", "İsabetli şut",     home.ShotsOnTarget,   away.ShotsOnTarget);
            Add("shotsOffTarget","İsabetsiz şut",    home.ShotsOffTarget,  away.ShotsOffTarget);
            Add("blockedShots",  "Bloke şut",        home.BlockedShots,    away.BlockedShots);
            Add("corners",       "Korner",           home.Corners,         away.Corners);
            Add("offsides",      "Ofsayt",           home.Offsides,        away.Offsides);
            Add("fouls",         "Faul",             home.Fouls,           away.Fouls);
            Add("yellow",        "Sarı kart",        home.YellowCards,     away.YellowCards);
            Add("red",           "Kırmızı kart",     home.RedCards,        away.RedCards);
            Add("saves",         "Kaleci kurtarışı", home.GoalkeeperSaves, away.GoalkeeperSaves);
            Add("passes",        "Pas",              home.TotalPasses,     away.TotalPasses);
            Add("accuratePasses","Başarılı pas",     home.AccuratePasses,  away.AccuratePasses);
            Add("passAccuracy",  "Başarılı pas %",   home.PassAccuracy,    away.PassAccuracy, percentage: true);

            // HİÇ ÖLÇÜM YOKSA VERİ DE YOKTUR — boş bir tablo "0-0 istatistik" değildir.
            var sourceKey = home.Source != null && home.Source.StartsWith("official:") ? home.Source.Substring("official:".Length) : null;
            return rows.Count == 0 ? null : new MatchStatisticsDto
            {
                Rows = rows,
                NotPublished = notPublished,
                SourceName = sourceKey == null ? null : Formax.Application.Services.OfficialSources.OfficialSourceRegistry.ByKey(sourceKey)?.Organization
            };
        }

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
