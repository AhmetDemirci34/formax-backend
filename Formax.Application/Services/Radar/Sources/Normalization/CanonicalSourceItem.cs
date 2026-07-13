using System;

namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — the canonical, normalized form of a collected
    /// item. This is the single shape the Intelligence layer (R.9+) will consume from
    /// staging. Produced only after structural normalization AND entity resolution
    /// succeed.
    ///
    /// This sprint builds the model and the producer; it is NOT persisted yet
    /// (staging is a later sprint).
    /// </summary>
    public sealed class CanonicalSourceItem
    {
        public string SourceKey { get; init; } = string.Empty;

        /// <summary>Source-local identity (carried through for de-duplication later).</summary>
        public string RawId { get; init; } = string.Empty;

        /// <summary>Kind of entity this item is about, e.g. "team", "news", "fixture".</summary>
        public string EntityType { get; init; } = string.Empty;

        /// <summary>Original name as it appeared in the raw item (e.g. "Man Utd").</summary>
        public string RawName { get; init; } = string.Empty;

        /// <summary>Resolved canonical name (e.g. "Manchester United").</summary>
        public string CanonicalName { get; init; } = string.Empty;

        /// <summary>Resolved FORMAX team id when the entity is a team; null otherwise.</summary>
        public int? ResolvedTeamId { get; init; }

        public DateTime? OccurredAtUtc { get; init; }

        /// <summary>Opaque raw payload carried through normalization.</summary>
        public string? Payload { get; init; }

        public DateTime NormalizedAtUtc { get; init; }
    }
}
