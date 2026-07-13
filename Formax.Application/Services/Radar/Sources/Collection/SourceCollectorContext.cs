using System;
using Formax.Domain.Entities;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — everything a collector needs to run one source,
    /// assembled by the dispatcher. Collectors read this and never reach back into the
    /// registry or DI directly.
    /// </summary>
    public sealed class SourceCollectorContext
    {
        public SourceDefinition Definition { get; init; } = default!;

        public string SourceKey => Definition.SourceKey;
        public SourceType Type => Definition.Type;
        public SourceCategory Category => Definition.Category;
        public string? Endpoint => Definition.Endpoint;

        /// <summary>Cycle timestamp from the scheduler.</summary>
        public DateTime NowUtc { get; init; }
    }
}
