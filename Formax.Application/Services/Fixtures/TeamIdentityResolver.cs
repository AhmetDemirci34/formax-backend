using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Formax.Application.Services.Fixtures
{
    /// <summary>
    /// FORMAX Data Engine v1 — Team Identity Engine. Farklı kaynakların farklı yazdığı
    /// takım adlarını TEK kanonik isme indirger (Man Utd / MUFC → Manchester United).
    /// Böylece aynı maç farklı kaynaklarda aynı kimliğe çözülür.
    /// </summary>
    public sealed class TeamIdentityResolver
    {
        // Kanonik ad → bilinen varyantlar. Yeni takım eklemek = tek satır.
        private static readonly Dictionary<string, string[]> Canonical =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Manchester United"] = new[] { "Man Utd", "Man United", "Manchester U", "MUFC", "Man U" },
                ["Manchester City"] = new[] { "Man City", "MCFC", "Man C" },
                ["Tottenham Hotspur"] = new[] { "Tottenham", "Spurs", "THFC" },
                ["Newcastle United"] = new[] { "Newcastle", "NUFC" },
                ["West Ham United"] = new[] { "West Ham", "WHU" },
                ["Wolverhampton Wanderers"] = new[] { "Wolves", "Wolverhampton" },
                ["Brighton & Hove Albion"] = new[] { "Brighton" },
                ["Arsenal"] = new[] { "AFC", "The Gunners" },
                ["Liverpool"] = new[] { "LFC" },
                ["Chelsea"] = new[] { "CFC" },

                ["Paris Saint-Germain"] = new[] { "PSG", "Paris SG", "Paris" },
                ["Bayern Munich"] = new[] { "Bayern", "FC Bayern", "Bayern München", "Bayern Munchen" },
                ["Borussia Dortmund"] = new[] { "Dortmund", "BVB" },
                ["Inter Milan"] = new[] { "Inter", "Internazionale", "FC Internazionale" },
                ["AC Milan"] = new[] { "Milan" },
                ["Juventus"] = new[] { "Juve" },
                ["Real Madrid"] = new[] { "Real Madrid CF" },
                ["Atletico Madrid"] = new[] { "Atletico", "Atleti", "Atlético Madrid", "Atlético de Madrid" },
                ["Barcelona"] = new[] { "Barca", "Barça", "FC Barcelona" },

                ["Galatasaray"] = new[] { "Galatasaray SK", "GS" },
                ["Fenerbahçe"] = new[] { "Fenerbahce", "Fenerbahçe SK", "FB" },
                ["Beşiktaş"] = new[] { "Besiktas", "Beşiktaş JK", "BJK" },
                ["Trabzonspor"] = new[] { "Trabzon", "TS" },
            };

        // Varyant(normalize) → kanonik (ters indeks, bir kez kurulur).
        private static readonly Dictionary<string, string> VariantToCanonical = BuildIndex();

        private static Dictionary<string, string> BuildIndex()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (canon, variants) in Canonical)
            {
                map[Key(canon)] = canon;
                foreach (var v in variants) map[Key(v)] = canon;
            }
            return map;
        }

        /// <summary>Ham takım adını kanonik isme çevirir. Bilinmiyorsa temizlenmiş adı döner.</summary>
        public string Resolve(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var cleaned = Clean(raw);
            return VariantToCanonical.TryGetValue(Key(cleaned), out var canon) ? canon : cleaned;
        }

        // Aynı kimliği üretmek için kullanılan slug (kıyas/hash için).
        public string Slug(string raw)
        {
            var canon = Resolve(raw);
            var sb = new StringBuilder();
            foreach (var ch in canon.ToUpperInvariant())
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.ToString();
        }

        private static string Clean(string s) =>
            string.Join(' ', s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        // Diakritik/boşluk/noktalama bağımsız anahtar.
        private static string Key(string s)
        {
            var norm = s.Normalize(NormalizationForm.FormD).ToLowerInvariant();
            var sb = new StringBuilder();
            foreach (var ch in norm)
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
