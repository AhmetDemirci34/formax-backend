using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — maps a <see cref="SourceType"/> to the collector
    /// that handles it. Built by the factory from the registered collectors; exposed
    /// as a model for diagnostics/inspection.
    /// </summary>
    public sealed class CollectorRegistration
    {
        public SourceType Type { get; init; }
        public ISourceCollector Collector { get; init; } = default!;
        public string CollectorName { get; init; } = string.Empty;
    }
}
