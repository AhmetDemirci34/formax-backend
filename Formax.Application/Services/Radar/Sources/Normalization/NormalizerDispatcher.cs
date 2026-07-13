using System;
using Formax.Application.Services.Radar.Sources.Collection;
using Formax.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — default dispatcher. Builds the
    /// <see cref="NormalizationContext"/> and runs the normalizer over the collector
    /// items. Does not persist; the canonical items and unresolved queue are returned
    /// for staging in a later sprint.
    /// </summary>
    public sealed class NormalizerDispatcher : INormalizerDispatcher
    {
        private readonly ISourceNormalizer _normalizer;
        private readonly ILogger<NormalizerDispatcher> _logger;

        public NormalizerDispatcher(
            ISourceNormalizer normalizer,
            ILogger<NormalizerDispatcher> logger)
        {
            _normalizer = normalizer;
            _logger = logger;
        }

        public SourceNormalizationResult Dispatch(
            SourceDefinition definition, SourceCollectorResult collectorResult, DateTime nowUtc)
        {
            if (!collectorResult.Success)
            {
                _logger.LogDebug(
                    "[RADAR NORMALIZER] {Key} skipped — collector failed: {Error}",
                    definition.SourceKey, collectorResult.Error);
                return SourceNormalizationResult.Empty(definition.SourceKey, nowUtc);
            }

            var context = new NormalizationContext
            {
                Definition = definition,
                NowUtc = nowUtc
            };

            var result = _normalizer.Normalize(context, collectorResult.Items);

            _logger.LogDebug(
                "[RADAR NORMALIZER] {Key} → canonical={Canon} unresolved={Unres} invalid={Invalid}",
                definition.SourceKey, result.NormalizedCount, result.UnresolvedCount, result.InvalidCount);

            return result;
        }
    }
}
