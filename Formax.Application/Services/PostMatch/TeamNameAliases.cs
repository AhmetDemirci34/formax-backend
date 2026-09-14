using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.News.Intelligence;

namespace Formax.Application.Services.PostMatch
{
    /// <summary>
    /// KONTROLLÜ TAKIM TAKMA ADI TABLOSU — video başlıklarında resmî kaynakların gerçekten kullandığı kısa
    /// adlar. Serbest benzerlik değildir: yalnız burada yazan eşleşmeler kabul edilir. Anahtar ve değerler
    /// katlanmış (diakritiksiz, küçük harf) biçimdedir.
    /// </summary>
    public static class TeamNameAliases
    {
        private static readonly Dictionary<string, string[]> Map = new(StringComparer.Ordinal)
        {
            ["nottingham forest"] = new[] { "nott m forest", "nottm forest" },
            ["manchester united"] = new[] { "man utd", "man united" },
            ["manchester city"] = new[] { "man city" },
            ["tottenham"] = new[] { "spurs", "tottenham hotspur" },
            ["wolves"] = new[] { "wolverhampton" },
            ["brighton"] = new[] { "brighton hove albion", "brighton and hove albion" },
            ["sheffield utd"] = new[] { "sheffield united" },
            ["west brom"] = new[] { "west bromwich" },
            ["qpr"] = new[] { "queens park rangers" },
            ["inter"] = new[] { "internazionale", "inter milan" },
            ["ac milan"] = new[] { "milan" },
            ["as roma"] = new[] { "roma" },
            ["bayern munchen"] = new[] { "bayern munich", "fc bayern" },
            ["borussia monchengladbach"] = new[] { "gladbach" },
            ["paris saint germain"] = new[] { "psg", "paris sg" },
            ["atletico madrid"] = new[] { "atleti" },
            ["athletic club"] = new[] { "athletic bilbao" },
            ["celta vigo"] = new[] { "rc celta", "celta" },
            ["malaga"] = new[] { "malaga cf" },
            ["psv eindhoven"] = new[] { "psv" },
            ["besiktas"] = new[] { "bjk" },
            ["genclerbirligi s k"] = new[] { "genclerbirligi" },
        };

        /// <summary>Takımın katlanmış takma adları (tablo dışı takım için boş).</summary>
        public static IReadOnlyList<string> For(string? teamName)
        {
            var folded = System.Text.RegularExpressions.Regex.Replace(NewsTextNormalizer.Fold(teamName), @"[^a-z0-9]+", " ").Trim();
            return Map.TryGetValue(folded, out var aliases) ? aliases : Array.Empty<string>();
        }

        /// <summary>Katlanmış metin takımı adı ya da tablodaki takma adıyla anıyor mu?</summary>
        public static bool Mentions(string foldedText, string? teamName)
        {
            if (NewsTextNormalizer.Mentions(foldedText, teamName)) return true;
            var spaced = System.Text.RegularExpressions.Regex.Replace(foldedText, @"[^a-z0-9]+", " ");
            return For(teamName).Any(a => (" " + spaced + " ").Contains(" " + a + " ", StringComparison.Ordinal));
        }
    }
}
