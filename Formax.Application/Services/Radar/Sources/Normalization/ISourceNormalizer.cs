using System.Collections.Generic;
using Formax.Application.Services.Radar.Sources.Collection;

namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — turns raw collector items into canonical items
    /// plus an unresolved queue. Pure: no I/O, no persistence.
    /// </summary>
    public interface ISourceNormalizer
    {
        SourceNormalizationResult Normalize(
            NormalizationContext context, IReadOnlyList<SourceCollectorItem> items);
    }
}
