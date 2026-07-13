using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.4) — enrichment derived from staged source items,
    /// attached to <see cref="MatchContextData"/>. Carries categorized items and counts.
    /// Empty when staging has no matching data (e.g. before real collectors run).
    /// </summary>
    public sealed class ContextEnrichmentResult
    {
        public IReadOnlyList<ContextEnrichmentItem> Items { get; init; }
            = Array.Empty<ContextEnrichmentItem>();

        public int NewsCount => Items.Count(i => i.Source == ContextEnrichmentSource.News);
        public int InternalSignalCount => Items.Count(i => i.Source == ContextEnrichmentSource.InternalSignal);
        public int MetadataCount => Items.Count(i => i.Source == ContextEnrichmentSource.SourceMetadata);

        public int TotalApplied => Items.Count;
        public bool HasEnrichment => Items.Count > 0;

        public DateTime EnrichedAtUtc { get; init; }

        public static ContextEnrichmentResult Empty(DateTime nowUtc)
            => new() { EnrichedAtUtc = nowUtc };
    }

    /// <summary>One enrichment fact mapped from a staged source item.</summary>
    public sealed class ContextEnrichmentItem
    {
        public ContextEnrichmentSource Source { get; init; }
        public string SourceKey { get; init; } = string.Empty;
        public int? RelatedTeamId { get; init; }
        public string CanonicalName { get; init; } = string.Empty;
        public string? Payload { get; init; }
        public DateTime NormalizedAtUtc { get; init; }
    }
}
