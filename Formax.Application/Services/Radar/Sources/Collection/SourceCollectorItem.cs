using System;
using System.Collections.Generic;

namespace Formax.Application.Services.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — a single raw item produced by a collector,
    /// before any normalization. Deliberately schema-light: the Normalizer (later
    /// sprint) turns these into canonical staging items.
    ///
    /// This sprint produces none (collectors are dummies); the shape exists so the
    /// dispatch contract is complete.
    /// </summary>
    public sealed class SourceCollectorItem
    {
        /// <summary>Source-local identity used later for de-duplication.</summary>
        public string RawId { get; init; } = string.Empty;

        /// <summary>Opaque raw payload (e.g. RSS item body, JSON fragment). Null allowed.</summary>
        public string? RawPayload { get; init; }

        /// <summary>When the underlying event/content occurred, if known.</summary>
        public DateTime? OccurredAtUtc { get; init; }

        /// <summary>Loose key/value extras the normalizer may use.</summary>
        public IReadOnlyDictionary<string, string>? Fields { get; init; }
    }
}
