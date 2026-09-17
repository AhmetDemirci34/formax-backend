using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Formax.Application.Services.OfficialSources
{
    /// <summary>
    /// TAKIM ADI EŞLEŞTİRİCİ — resmî kaynağın yazdığı ad ile FORMAX'ın takım adını karşılaştırır.
    ///
    /// Resmî kaynaklar ticari/tüzel adları kullanır: "BEŞİKTAŞ A.Ş.", "ÇAYKUR RİZESPOR A.Ş.",
    /// "Brighton and Hove Albion", "AC Milan"/"Milan". Kural: iki ad aksanları katlanıp tüzel
    /// ekler (A.Ş., FK, FC, SK…) atıldıktan sonra, KISA adın bütün anlamlı sözcükleri uzun
    /// adda geçiyorsa aynı takımdır ("rizespor" ⊆ "caykur rizespor"). Kısmi sözcük (önek)
    /// eşleşmesi YOKTUR: "Manchester United" ile "Manchester City" eşleşmez.
    ///
    /// Bu tek başına kimlik değildir: kimlik çözücü aynı ligde, aynı tarih penceresinde EV ve
    /// DEPLASMANIN birlikte eşleşmesini ister.
    /// </summary>
    public static class OfficialTeamNameMatcher
    {
        /// <summary>Tüzel/biçimsel ekler — takımı ayırt etmez.</summary>
        private static readonly HashSet<string> Noise = new(StringComparer.Ordinal)
        {
            "fc", "fk", "sk", "as", "a", "s", "ac", "afc", "cf", "sc", "ssc", "acf", "us", "ss", "cfc",
            "club", "kulubu", "futbol", "the", "and", "de", "calcio", "sad", "sd", "cd", "ud", "rcd",
            "jk", "sportif", "faaliyetler", "anonim", "sirketi",
            // İspanyolca/Galiçyaca tanımlık: LALIGA "Real Club Deportivo de A Coruña" ↔ FORMAX "Deportivo La Coruna" (15.09.2026).
            "la"
        };

        /// <summary>
        /// Ölçülmüş takma adlar (resmî ad ↔ FORMAX adı tek sözcükle karşılanamadığında).
        /// Anahtar ve değer katlanmış biçimdedir; eşleşme iki yönlüdür.
        /// </summary>
        private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
        {
            ["wolves"] = "wolverhampton",
            ["spurs"] = "tottenham",
            ["man utd"] = "manchester united",
            ["man city"] = "manchester city",
            ["inter"] = "internazionale",
            ["psg"] = "paris saint germain",
            // UEFA uluslararası yazımı (17.09.2026 ölçümü) — FORMAX kayıt adı
            ["olympiacos"] = "olympiakos piraeus",
            // LALIGA tüzel/kısa adı (17.09.2026 ölçümü: Barcelona 7-2 "Real Racing Club SAD" / "R. Racing Club") — FORMAX "Racing Santander"
            ["real racing club sad"] = "racing santander",
            ["real racing club"] = "racing santander",
            ["r racing club"] = "racing santander"
        };

        /// <summary>Aksan katlama + küçük harf + noktalama temizliği.</summary>
        public static string Fold(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                // Türkçe İ/ı ToLowerInvariant'ta "i̇" üretir (ölçülmüş tuzak) — önce elle çevrilir.
                switch (ch)
                {
                    case 'İ': case 'I': case 'ı': sb.Append('i'); continue;
                    case 'Ş': case 'ş': sb.Append('s'); continue;
                    case 'Ğ': case 'ğ': sb.Append('g'); continue;
                    case 'Ç': case 'ç': sb.Append('c'); continue;
                    case 'Ö': case 'ö': sb.Append('o'); continue;
                    case 'Ü': case 'ü': sb.Append('u'); continue;
                    case 'ß': sb.Append("ss"); continue;
                    case 'Ø': case 'ø': sb.Append('o'); continue;
                    case 'Đ': case 'đ': sb.Append('d'); continue;
                    case 'Ł': case 'ł': sb.Append('l'); continue;
                }
                sb.Append(ch);
            }

            var decomposed = sb.ToString().Normalize(NormalizationForm.FormD);
            var clean = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) clean.Append(char.ToLowerInvariant(ch));
                else clean.Append(' ');
            }
            return string.Join(' ', clean.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>Anlamlı sözcükler (tüzel ekler atılmış).</summary>
        public static IReadOnlyList<string> Tokens(string? value)
        {
            var folded = Fold(value);
            if (Aliases.TryGetValue(folded, out var alias)) folded = alias;
            return folded.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                         .Where(t => !Noise.Contains(t))
                         .ToList();
        }

        /// <summary>İki ad aynı takımı mı gösteriyor?</summary>
        public static bool SameTeam(string? a, string? b)
        {
            var fa = Fold(a);
            var fb = Fold(b);
            if (fa.Length == 0 || fb.Length == 0) return false;
            if (fa == fb) return true;
            if (Aliases.TryGetValue(fa, out var aa) && aa == fb) return true;
            if (Aliases.TryGetValue(fb, out var ab) && ab == fa) return true;

            var ta = Tokens(a);
            var tb = Tokens(b);
            if (ta.Count == 0 || tb.Count == 0) return false;

            var (shorter, longer) = ta.Count <= tb.Count ? (ta, tb) : (tb, ta);
            var set = new HashSet<string>(longer, StringComparer.Ordinal);
            return shorter.All(set.Contains);
        }
    }
}
