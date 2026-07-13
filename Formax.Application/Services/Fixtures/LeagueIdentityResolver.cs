using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Formax.Application.Services.Fixtures
{
    /// <summary>
    /// FORMAX Data Engine v1 — League Identity Engine. Farklı kaynakların lig adlarını
    /// (EPL / English Premier League / Premier League) TEK kanonik isme indirger ve
    /// kanonik lig için ülkeyi çıkarır.
    /// </summary>
    public sealed class LeagueIdentityResolver
    {
        // Kanonik lig → (varyantlar, ülke).
        private static readonly Dictionary<string, (string[] Variants, string Country)> Canonical =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Premier League"] = (new[] { "EPL", "English Premier League", "Premier League England", "Barclays Premier League" }, "England"),
                ["Championship"] = (new[] { "EFL Championship", "English Championship", "Sky Bet Championship" }, "England"),
                ["La Liga"] = (new[] { "LaLiga", "Primera Division", "Spanish La Liga", "La Liga Santander", "Spanish Primera" }, "Spain"),
                ["Serie A"] = (new[] { "Italian Serie A", "Serie A TIM" }, "Italy"),
                ["Bundesliga"] = (new[] { "German Bundesliga", "1. Bundesliga", "Fussball Bundesliga" }, "Germany"),
                ["Ligue 1"] = (new[] { "French Ligue 1", "Ligue 1 Uber Eats" }, "France"),
                ["Eredivisie"] = (new[] { "Dutch Eredivisie" }, "Netherlands"),
                ["Primeira Liga"] = (new[] { "Liga Portugal", "Portuguese Primeira Liga", "Liga NOS" }, "Portugal"),
                ["Süper Lig"] = (new[] { "Super Lig", "Turkish Super Lig", "Trendyol Süper Lig", "Spor Toto Super Lig" }, "Turkey"),
                ["Champions League"] = (new[] { "UEFA Champions League", "UCL" }, "Europe"),
                ["Europa League"] = (new[] { "UEFA Europa League", "UEL" }, "Europe"),
                ["World Cup"] = (new[] { "FIFA World Cup", "World Cup Finals" }, "World"),
            };

        private static readonly Dictionary<string, (string Canon, string Country)> VariantIndex = BuildIndex();

        private static Dictionary<string, (string, string)> BuildIndex()
        {
            var map = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            foreach (var (canon, info) in Canonical)
            {
                map[Key(canon)] = (canon, info.Country);
                foreach (var v in info.Variants) map[Key(v)] = (canon, info.Country);
            }
            return map;
        }

        /// <summary>Kanonik lig adı (bilinmiyorsa temizlenmiş ham ad).</summary>
        public string Resolve(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var cleaned = raw.Trim();
            return VariantIndex.TryGetValue(Key(cleaned), out var hit) ? hit.Canon : cleaned;
        }

        /// <summary>Lig için ülke (provider ülke vermediyse fallback). Bilinmiyorsa "".</summary>
        public string Country(string rawLeague, string? providerCountry = null)
        {
            if (!string.IsNullOrWhiteSpace(providerCountry)) return providerCountry.Trim();
            return VariantIndex.TryGetValue(Key(rawLeague ?? ""), out var hit) ? hit.Country : "";
        }

        public string Slug(string raw)
        {
            var canon = Resolve(raw);
            var sb = new StringBuilder();
            foreach (var ch in canon.ToUpperInvariant())
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.ToString();
        }

        private static string Key(string s)
        {
            var norm = (s ?? "").Normalize(NormalizationForm.FormD).ToLowerInvariant();
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
