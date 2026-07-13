using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — default dispatcher. Bridges scheduler → factory →
    /// collector. The collector's items are NOT persisted in this sprint (no normalizer
    /// / staging yet); the dispatcher returns the raw result for the scheduler to log.
    /// </summary>
    public sealed class SourceCollectorDispatcher : ISourceCollectorDispatcher
    {
        private readonly ISourceCollectorFactory _factory;
        private readonly ILogger<SourceCollectorDispatcher> _logger;

        public SourceCollectorDispatcher(
            ISourceCollectorFactory factory,
            ILogger<SourceCollectorDispatcher> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<SourceCollectorResult> DispatchAsync(
            SourceDefinition definition, DateTime nowUtc, CancellationToken ct = default)
        {
            var collector = _factory.Resolve(definition);
            if (collector is null)
            {
                _logger.LogWarning(
                    "[RADAR DISPATCHER] no collector registered for type {Type} (source {Key}).",
                    definition.Type, definition.SourceKey);
                return SourceCollectorResult.Fail(
                    definition.SourceKey,
                    $"no collector for type {definition.Type}",
                    0,
                    nowUtc);
            }

            var context = new SourceCollectorContext
            {
                Definition = definition,
                NowUtc = nowUtc
            };

            // Base guarantees no-throw; this is defensive only.
            return await collector.CollectAsync(context, ct);
        }
    }
}
