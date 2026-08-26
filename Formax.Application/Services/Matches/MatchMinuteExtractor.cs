using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// MAÇ DAKİKASI — yalnız KAYNAĞIN KENDİ METNİNDEN okunur.
    ///
    /// DAKİKA HESAPLANMAZ. Bir içeriğin yayın saati ile ilk düdük arasındaki farktan
    /// "32. dakika" ÜRETİLMEZ: ajans olaydan sonra yazar, canlı anlatım gecikir, sosyal
    /// paylaşım devre arasında gelir. Kaynak açıkça bir dakika yazmıyorsa dakika YOKTUR.
    ///
    /// TASARIM İLKESİ: yanlış dakika, dakikasızlıktan kötüdür. Bu yüzden kalıplar
    /// KASITLI olarak dardır — şüpheli her biçim reddedilir.
    ///
    /// GERÇEK BAŞLIKLARDA ÖLÇÜLEN TUZAKLAR (hepsi reddedilir):
    ///   • "Fenerbahçe - Lyon maçının 11'leri belli oldu"   → kadro sayısı, dakika değil
    ///   • "kritik 90 dakika ve muhtemel 11'ler"            → maç süresi, dakika değil
    ///   • "Saat 21.30'da ilk düdük"                        → saat, dakika değil
    ///   • "17 yıllık hasret 180 dakika uzaklıkta"          → süre + aralık dışı
    /// GEÇERLİ ÖRNEK:
    ///   • "Samsunspor - Göztepe Maçında Gol: … (30. dakika)" → 30
    /// </summary>
    public static class MatchMinuteExtractor
    {
        private const RegexOptions Opts =
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        /// <summary>
        /// Sayının önünde saat/ondalık bağlamı olmamalı: "21.30'da", "2:45" gibi
        /// yapılardaki ikinci parça dakika DEĞİLDİR.
        /// </summary>
        private const string NoClockPrefix = @"(?<![\d.,:])";

        /// <summary>"45+2'" / "90+4. dakika" — uzatma dakikası.</summary>
        private static readonly Regex AddedTime = new(
            NoClockPrefix + @"\b(\d{1,3})\s*\+\s*(\d{1,2})\s*(?:['’′]|\.\s*(?:dakika\w*|dk)\b|\s*minute\b)", Opts);

        /// <summary>
        /// "67'" / "90'da" — ama "11'ler" DEĞİL. Apostroftan sonraki ek denetlenir:
        /// yalnız zaman eki (da/de/ta/te, inci…) veya ek YOKSA dakikadır.
        /// </summary>
        private static readonly Regex Apostrophe = new(
            NoClockPrefix + @"\b(\d{1,3})\s*['’′](\p{L}*)", Opts);

        private static readonly string[] TimeSuffixes =
            { "", "da", "de", "ta", "te", "inci", "ıncı", "uncu", "üncü", "nci", "ncı", "ncu", "ncü" };

        private static readonly CultureInfo Tr = new("tr-TR");

        /// <summary>
        /// KADRO BAĞLAMI — sayıdan hemen önce bu kelimeler varsa sayı dakika DEĞİLDİR.
        ///
        /// Ölçüldü (gerçek başlık): "Fenerbahçe, Olimpik Lyon'u ağırladı: İlk 11'de
        /// değişiklikler" → "11'de" zaman ekiyle bitiyor ama kastedilen İLK ON BİR'dir.
        /// Aynı şekilde "ilk 45'te" = ilk yarı, dakika 45 değil.
        /// </summary>
        private static readonly Regex LineupContext = new(
            @"(ilk|muhtemel|kadro|dizili\w*|başlangıç|baslangic)\s+$", Opts);

        /// <summary>Sayının hemen öncesinde kadro/yarı bağlamı var mı?</summary>
        private static bool HasLineupContext(string raw, int index)
        {
            var start = Math.Max(0, index - 24);
            var before = raw.Substring(start, index - start).ToLower(Tr);
            return LineupContext.IsMatch(before);
        }

        /// <summary>
        /// "30. dakika" / "45. dakikada" — NOKTA ZORUNLUDUR (sıra sayısı).
        /// Noktasız "90 dakika" maç SÜRESİDİR, dakika değildir.
        /// </summary>
        private static readonly Regex TurkishOrdinal = new(
            NoClockPrefix + @"\b(\d{1,3})\s*\.\s*(?:dakikasında|dakikada|dakikasi|dakika|dk)\b", Opts);

        /// <summary>"dk 67" / "dk. 67" — önek biçimi tek anlamlıdır.</summary>
        private static readonly Regex TurkishPrefix = new(@"\bdk\.?\s*(\d{1,3})\b", Opts);

        /// <summary>"67th minute" / "minute 67" — sıra eki ya da önek zorunlu.</summary>
        private static readonly Regex EnglishMinute = new(
            NoClockPrefix + @"\b(?:(\d{1,3})\s*(?:st|nd|rd|th)\s*minute|minute\s*(\d{1,3}))\b", Opts);

        /// <summary>
        /// Metinde AÇIKÇA yazan maç dakikası. Bulunamazsa (null, null).
        /// Label uzatmayı korur ("45+2"); Value sıralama/karşılaştırma içindir.
        /// </summary>
        public static (int? Value, string? Label) Extract(params string?[] texts)
        {
            foreach (var raw in texts)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var added = AddedTime.Match(raw);
                if (added.Success
                    && TryMinute(added.Groups[1].Value, out var baseMin)
                    && int.TryParse(added.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var extra)
                    && extra is > 0 and <= 30)
                {
                    return (baseMin + extra, $"{baseMin}+{extra}");
                }

                var ordinal = TurkishOrdinal.Match(raw);
                if (ordinal.Success && TryMinute(ordinal.Groups[1].Value, out var m1))
                    return (m1, m1.ToString(CultureInfo.InvariantCulture));

                var prefix = TurkishPrefix.Match(raw);
                if (prefix.Success && TryMinute(prefix.Groups[1].Value, out var m2))
                    return (m2, m2.ToString(CultureInfo.InvariantCulture));

                var en = EnglishMinute.Match(raw);
                if (en.Success)
                {
                    var digits = en.Groups[1].Success ? en.Groups[1].Value : en.Groups[2].Value;
                    if (TryMinute(digits, out var m3))
                        return (m3, m3.ToString(CultureInfo.InvariantCulture));
                }

                // Apostrof kalıbı EN SONDA: en gürültülü biçim odur, ekine bakılarak doğrulanır.
                foreach (Match hit in Apostrophe.Matches(raw))
                {
                    if (!IsTimeSuffix(hit.Groups[2].Value)) continue;
                    if (HasLineupContext(raw, hit.Index)) continue;
                    if (TryMinute(hit.Groups[1].Value, out var m4))
                        return (m4, m4.ToString(CultureInfo.InvariantCulture));
                }
            }

            return (null, null);
        }

        /// <summary>Apostroftan sonraki ek bir ZAMAN eki mi (tr-TR küçültme; I/İ tuzağı)?</summary>
        private static bool IsTimeSuffix(string suffix)
        {
            if (suffix.Length == 0) return true;
            var s = suffix.ToLower(new CultureInfo("tr-TR"));
            return Array.IndexOf(TimeSuffixes, s) >= 0;
        }

        /// <summary>Geçerli maç dakikası aralığı: 1–120 (uzatma dâhil).</summary>
        private static bool TryMinute(string digits, out int minute)
            => int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out minute)
               && minute is > 0 and <= 120;
    }
}
