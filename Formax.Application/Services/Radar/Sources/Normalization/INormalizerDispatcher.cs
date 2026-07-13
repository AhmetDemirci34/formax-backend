using System;
using Formax.Application.Services.Radar.Sources.Collection;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — entry point that takes a collector result and
    /// runs it through the normalizer. This is the Collector → Normalizer seam; the
    /// scheduler is untouched in this sprint, so this path is exercised via tests /
    /// later wiring, not the live loop.
    /// </summary>
    public interface INormalizerDispatcher
    {
        SourceNormalizationResult Dispatch(
            SourceDefinition definition, SourceCollectorResult collectorResult, DateTime nowUtc);
    }
}
