using System;
using System.Collections.Generic;

namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — outcome of normalizing one collector result.
    /// Splits the input into resolved canonical items and an unresolved queue; nothing
    /// is silently discarded except structurally invalid items (counted separately).
    /// </summary>
    public sealed class SourceNormalizationResult
    {
        public string SourceKey { get; init; } = string.Empty;

        public IReadOnlyList<CanonicalSourceItem> Canonical { get; init; }
            = Array.Empty<CanonicalSourceItem>();

        public IReadOnlyList<UnresolvedSourceItem> Unresolved { get; init; }
            = Array.Empty<UnresolvedSourceItem>();

        public int NormalizedCount => Canonical.Count;
        public int UnresolvedCount => Unresolved.Count;

        /// <summary>Items dropped for being structurally invalid.</summary>
        public int InvalidCount { get; init; }

        public DateTime NormalizedAtUtc { get; init; }

        public static SourceNormalizationResult Empty(string sourceKey, DateTime nowUtc)
            => new() { SourceKey = sourceKey, NormalizedAtUtc = nowUtc };
    }
}
