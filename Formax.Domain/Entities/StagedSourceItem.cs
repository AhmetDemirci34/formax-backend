using System;
using Formax.Domain.Enums;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Radar Source Engine (R.8.5) — a normalized item parked in staging, awaiting
    /// consumption by the Intelligence layer (R.9+). This is the persisted form of a
    /// <c>CanonicalSourceItem</c>: the single hand-off point between the Source Engine
    /// and Intelligence. Nothing here writes to Match/Team or feeds Intelligence yet.
    /// </summary>
    public sealed class StagedSourceItem
    {
        public long Id { get; set; }

        /// <summary>Owning source definition id (R.8.1 SourceDefinition.Id).</summary>
        public int SourceId { get; set; }

        /// <summary>Source key, carried for traceability.</summary>
        public string SourceKey { get; set; } = string.Empty;

        public SourceCategory Category { get; set; }

        // ── Canonical payload ─────────────────────────────────────────────────
        public string RawId { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string RawName { get; set; } = string.Empty;
        public string CanonicalName { get; set; } = string.Empty;
        public int? ResolvedTeamId { get; set; }
        public string? Payload { get; set; }

        // ── Timestamps & lifecycle ────────────────────────────────────────────
        public DateTime CollectedAtUtc { get; set; }
        public DateTime NormalizedAtUtc { get; set; }

        /// <summary>TTL in seconds applied at write time.</summary>
        public int TtlSeconds { get; set; }

        /// <summary>NormalizedAt + TTL. Used for retention by a later sprint.</summary>
        public DateTime ExpiresAtUtc { get; set; }

        /// <summary>False at write; flipped true once Intelligence consumes it (R.9+).</summary>
        public bool Processed { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
