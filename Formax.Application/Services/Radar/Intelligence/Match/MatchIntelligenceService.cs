using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.3) — default service. Now driven by the enriched
    /// context: builds <see cref="MatchContextData"/> via the context builder, runs the
    /// rule-based <see cref="IMatchSignalEngine"/>, and persists a
    /// <see cref="MatchIntelligenceSnapshot"/>.
    ///
    /// Pipeline: Match → MatchContextData (form/H2H/importance) → Signal Engine →
    /// MatchSignal → snapshot.
    /// </summary>
    public sealed class MatchIntelligenceService : IMatchIntelligenceService
    {
        private readonly IMatchIntelligenceRepository _repository;
        private readonly IMatchContextBuilder _contextBuilder;
        private readonly IMatchContextEnricher _enricher;
        private readonly IMatchSignalEngine _signalEngine;
        private readonly IMatchImportanceEngine _importanceEngine;
        private readonly ILogger<MatchIntelligenceService> _logger;

        public MatchIntelligenceService(
            IMatchIntelligenceRepository repository,
            IMatchContextBuilder contextBuilder,
            IMatchContextEnricher enricher,
            IMatchSignalEngine signalEngine,
            IMatchImportanceEngine importanceEngine,
            ILogger<MatchIntelligenceService> logger)
        {
            _repository = repository;
            _contextBuilder = contextBuilder;
            _enricher = enricher;
            _signalEngine = signalEngine;
            _importanceEngine = importanceEngine;
            _logger = logger;
        }

        public async Task<MatchIntelligenceSnapshot?> BuildForMatchAsync(int matchId, CancellationToken ct = default)
        {
            var context = await _contextBuilder.BuildAsync(matchId, ct);
            if (context is null)
            {
                _logger.LogDebug("[MATCH INTEL] context for match {Id} unavailable.", matchId);
                return null;
            }

            // R.9.4 — live bridge: enrich the context from staged source data before
            // signal evaluation. Empty until real collectors populate staging.
            await _enricher.EnrichAsync(context, ct);

            var result = _signalEngine.Evaluate(context);

            // R.9.6 — first-class importance, independent of the signal engine.
            var importance = _importanceEngine.Evaluate(context);

            var snapshot = new MatchIntelligenceSnapshot
            {
                MatchId = matchId,
                Status = result.HasSignals ? MatchIntelligenceStatus.Active : MatchIntelligenceStatus.NoSignal,
                PrimarySignalType = result.PrimarySignalType,
                SignalCount = result.Signals.Count,
                SignalsJson = JsonSerializer.Serialize(result.Signals),
                Summary = result.Summary,
                ImportanceScore = importance.Score.Value,
                ImportanceLevel = importance.Score.Level,
                NewsImpactScore = context.NewsImpactScore,
                NewsImpactLevel = context.NewsImpactLevel,
                SyntheticSignalScore = context.SyntheticSignalScore,
                SyntheticSignalLevel = context.SyntheticSignalLevel,
                SyntheticDirection = context.SyntheticDirection,
                GeneratedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(snapshot, ct);
            await _repository.SaveChangesAsync(ct);

            return snapshot;
        }

        public async Task<int> BuildUpcomingAsync(DateTime fromUtc, CancellationToken ct = default)
        {
            var ids = await _repository.GetMatchIdsFromAsync(fromUtc, ct);
            var built = 0;
            foreach (var id in ids)
            {
                var snap = await BuildForMatchAsync(id, ct);
                if (snap is not null) built++;
            }
            return built;
        }
    }
}
