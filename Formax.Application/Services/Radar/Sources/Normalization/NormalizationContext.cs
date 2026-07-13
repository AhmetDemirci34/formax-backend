using System;
using Formax.Domain.Entities;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — inputs the normalizer needs to process one
    /// collector result, assembled by the dispatcher.
    /// </summary>
    public sealed class NormalizationContext
    {
        public SourceDefinition Definition { get; init; } = default!;

        public string SourceKey => Definition.SourceKey;
        public SourceCategory Category => Definition.Category;

        public DateTime NowUtc { get; init; }
    }
}
