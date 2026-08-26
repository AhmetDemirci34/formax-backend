using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// MAÇ TÜRÜ — "Bu maç ne için oynanıyor?" sorusunun DOĞRULANABİLİR cevabı.
    ///
    /// Kaynak yalnız sağlayıcının GERÇEK tur/aşama metnidir (`Match.Round`, yoksa
    /// `CompetitionContexts.StageName`). Burada tahmin, AI yorumu veya "önemlilik"
    /// değerlendirmesi YOKTUR; yalnız bilinen tur adının Türkçe karşılığı verilir.
    ///
    /// TANINMAYAN TUR = null. Uydurma aşama üretilmez ("Final", "Eleme", "Grup" gibi bir
    /// şey tahmin edilmez); UI o zaman tür satırını hiç göstermez.
    ///
    /// TEK GÜVENLİ GENELLEME: sağlayıcı turu "Regular Season"/"League Stage" diyorsa bu
    /// zaten ligin normal maçıdır → "Lig Maçı". Bu bir tahmin değil, tur adının çevirisidir.
    /// </summary>
    public static class MatchTypeLabelResolver
    {
        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        /// <summary>"Regular Season - 12" → 12. Hafta numarası yoksa null.</summary>
        private static readonly Regex WeekNumber = new(@"-\s*(\d{1,2})\s*$", Opts);

        /// <summary>"3rd Qualifying Round" → 3. Sıra numarası yoksa null.</summary>
        private static readonly Regex OrdinalPrefix = new(@"^\s*(\d{1,2})(?:st|nd|rd|th)?\b", Opts);

        /// <summary>
        /// Sağlayıcı tur metnini kullanıcıya gösterilecek Türkçe maç türüne çevirir.
        /// Tanınmayan/boş metinde null döner.
        /// </summary>
        public static string? Resolve(string? providerRound)
        {
            if (string.IsNullOrWhiteSpace(providerRound)) return null;

            var raw = providerRound.Trim();
            // Sıralama önemli: "Semi-finals" içinde "final" geçer, önce o yakalanmalı.
            var r = raw.ToLowerInvariant();

            if (r.Contains("3rd place") || r.Contains("third place"))
                return "3.'lük Maçı";

            if (r.Contains("semi"))
                return "Yarı Final";

            if (r.Contains("quarter"))
                return "Çeyrek Final";

            if (r.Contains("round of 16") || r.Contains("1/8"))
                return "Son 16 Turu";

            if (r.Contains("round of 32") || r.Contains("1/16"))
                return "Son 32 Turu";

            if (r.Contains("round of 64"))
                return "Son 64 Turu";

            // "Play-offs", "Playoff round", "Play Off"
            if (r.Contains("play-off") || r.Contains("playoff") || r.Contains("play off"))
                return "Play-off Turu";

            if (r.Contains("qualifying") || r.Contains("qualification"))
            {
                var n = Ordinal(raw);
                return n.HasValue ? $"{n}. Eleme Turu" : "Eleme Turu";
            }

            if (r.Contains("group"))
                return "Grup Aşaması";

            if (r.Contains("league stage") || r.Contains("league phase"))
                return "Lig Aşaması";

            if (r.Contains("regular season"))
            {
                var w = Week(raw);
                return w.HasValue ? $"Lig Maçı · {w}. Hafta" : "Lig Maçı";
            }

            // "Final" en sonda: yukarıdaki semi/quarter/3rd place dalları elendikten sonra
            // geriye kalan "final" gerçekten finaldir.
            if (r.Contains("final"))
                return "Final";

            // Tanınmayan tur adı → UYDURULMAZ.
            return null;
        }

        private static int? Week(string raw)
        {
            var m = WeekNumber.Match(raw);
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var n) ? n : null;
        }

        private static int? Ordinal(string raw)
        {
            var m = OrdinalPrefix.Match(raw);
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var n) ? n : null;
        }
    }
}
