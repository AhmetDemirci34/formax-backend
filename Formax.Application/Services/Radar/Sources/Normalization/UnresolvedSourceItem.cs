using System;

namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — a raw item that passed structural normalization
    /// but could NOT be resolved to a canonical entity. Kept (never discarded) so the
    /// data is recoverable once the alias table grows or an operator maps it manually.
    ///
    /// This sprint defines the queue model; durable persistence of the queue is a
    /// later sprint (staging forbidden here).
    /// </summary>
    public sealed class UnresolvedSourceItem
    {
        public string SourceKey { get; init; } = string.Empty;
        public string RawId { get; init; } = string.Empty;

        /// <summary>The unresolved name that needs an alias mapping.</summary>
        public string RawName { get; init; } = string.Empty;

        public string EntityType { get; init; } = string.Empty;

        /// <summary>Why resolution failed (e.g. "no alias match").</summary>
        public string Reason { get; init; } = string.Empty;

        public string? Payload { get; init; }

        public DateTime SeenAtUtc { get; init; }
    }
}
