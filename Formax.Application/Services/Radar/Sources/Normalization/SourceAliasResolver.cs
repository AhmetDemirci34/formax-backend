using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — default Layer-1 resolver.
    ///
    /// Builds two lookup indexes from the alias provider once at construction:
    ///   1. exact (raw alias string)
    ///   2. normalized (lowercased, accents stripped, common club suffixes removed)
    /// A raw name resolves on exact first, then normalized. No fuzzy matching.
    /// </summary>
    public sealed class SourceAliasResolver : ISourceAliasResolver
    {
        private static readonly string[] StripSuffixes =
            { " fc", " sk", " as", " ac", " cf", " sc" };

        private readonly Dictionary<string, SourceAlias> _exact = new();
        private readonly Dictionary<string, SourceAlias> _normalized = new();

        public SourceAliasResolver(ISourceAliasProvider provider)
        {
            foreach (var alias in provider.GetAliases())
            {
                if (string.IsNullOrWhiteSpace(alias.Alias)) continue;

                _exact.TryAdd(alias.Alias, alias);

                var norm = Normalize(alias.Alias);
                if (!string.IsNullOrEmpty(norm))
                    _normalized.TryAdd(norm, alias);

                // Also index the canonical name itself so it resolves to itself.
                var canonNorm = Normalize(alias.CanonicalName);
                if (!string.IsNullOrEmpty(canonNorm))
                    _normalized.TryAdd(canonNorm, alias);
            }
        }

        public AliasResolution Resolve(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return AliasResolution.Miss();

            if (_exact.TryGetValue(rawName, out var exact))
                return AliasResolution.Hit(exact);

            var norm = Normalize(rawName);
            if (!string.IsNullOrEmpty(norm) && _normalized.TryGetValue(norm, out var byNorm))
                return AliasResolution.Hit(byNorm);

            return AliasResolution.Miss();
        }

        /// <summary>Lowercase, strip accents, collapse whitespace, drop club suffixes.</summary>
        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var lowered = value.Trim().ToLowerInvariant();

            // Strip accents.
            var decomposed = lowered.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            var stripped = sb.ToString().Normalize(NormalizationForm.FormC);

            // Collapse internal whitespace.
            stripped = string.Join(' ', stripped.Split(' ', System.StringSplitOptions.RemoveEmptyEntries));

            foreach (var suffix in StripSuffixes)
            {
                if (stripped.EndsWith(suffix, System.StringComparison.Ordinal))
                {
                    stripped = stripped[..^suffix.Length].TrimEnd();
                    break;
                }
            }

            return stripped;
        }
    }
}
