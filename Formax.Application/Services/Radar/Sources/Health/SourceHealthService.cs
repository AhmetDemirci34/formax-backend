using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Sources.Health
{
    /// <summary>
    /// Radar Source Engine (R.8.6) — default health service. Maintains a per-source
    /// snapshot from execution outcomes: updates counters/timestamps, recomputes
    /// SuccessRate / FailureRate / AvgExecutionTimeMs and a 0..100 HealthScore, then
    /// classifies into a <see cref="SourceHealthStatus"/>. Pure measurement — no
    /// failover, no alerting (Monitor is a later sprint).
    /// </summary>
    public sealed class SourceHealthService : ISourceHealthService
    {
        private readonly ISourceHealthRepository _repository;
        private readonly ILogger<SourceHealthService> _logger;

        public SourceHealthService(
            ISourceHealthRepository repository,
            ILogger<SourceHealthService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<HealthCalculationResult> RecordExecutionAsync(
            SourceDefinition definition,
            bool success,
            double executionMs,
            DateTime nowUtc,
            CancellationToken ct = default)
        {
            try
            {
                var existing = await _repository.GetBySourceIdAsync(definition.Id, ct);
                var isNew = existing is null;

                var snap = existing ?? new SourceHealthSnapshot
                {
                    SourceId = definition.Id,
                    SourceKey = definition.SourceKey
                };

                // ── Counters ──────────────────────────────────────────────────
                var prevTotal = snap.TotalExecutions;
                snap.TotalExecutions = prevTotal + 1;
                snap.SourceKey = definition.SourceKey;

                if (success)
                {
                    snap.SuccessCount += 1;
                    snap.ConsecutiveFailures = 0;
                    snap.LastSuccessAtUtc = nowUtc;
                }
                else
                {
                    snap.FailureCount += 1;
                    snap.ConsecutiveFailures += 1;
                    snap.LastFailureAtUtc = nowUtc;
                }

                // ── Derived metrics ───────────────────────────────────────────
                snap.SuccessRate = snap.TotalExecutions == 0
                    ? 0
                    : (double)snap.SuccessCount / snap.TotalExecutions;
                snap.FailureRate = snap.TotalExecutions == 0
                    ? 0
                    : (double)snap.FailureCount / snap.TotalExecutions;

                // Running average over all executions.
                snap.AvgExecutionTimeMs = prevTotal == 0
                    ? executionMs
                    : ((snap.AvgExecutionTimeMs * prevTotal) + executionMs) / snap.TotalExecutions;

                var scored = ComputeScore(snap);
                snap.HealthScore = scored.Score;
                snap.Status = scored.Status;
                snap.UpdatedAtUtc = nowUtc;

                await _repository.UpsertAsync(snap, ct);
                await _repository.SaveChangesAsync(ct);

                return new HealthCalculationResult
                {
                    SourceId = definition.Id,
                    SourceKey = definition.SourceKey,
                    HealthScore = snap.HealthScore,
                    Status = snap.Status,
                    Outcome = isNew ? HealthCalculationOutcome.Created : HealthCalculationOutcome.Updated
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RADAR HEALTH] {Key} record failed.", definition.SourceKey);
                return HealthCalculationResult.Failed(definition.Id, definition.SourceKey, ex.Message);
            }
        }

        public Task<SourceHealthSnapshot?> GetAsync(int sourceId, CancellationToken ct = default)
            => _repository.GetBySourceIdAsync(sourceId, ct);

        /// <summary>
        /// HealthScore = SuccessRate × 100, penalised for sustained consecutive failures
        /// (10 points each, capped at 40). Status thresholds: ≥80 Healthy, ≥50 Degraded,
        /// otherwise Unhealthy; Unknown when no executions exist.
        /// </summary>
        private static SourceHealthScore ComputeScore(SourceHealthSnapshot snap)
        {
            if (snap.TotalExecutions == 0)
                return SourceHealthScore.Of(0, SourceHealthStatus.Unknown);

            var basePoints = snap.SuccessRate * 100.0;
            var penalty = Math.Min(40, snap.ConsecutiveFailures * 10);
            var score = Math.Clamp(basePoints - penalty, 0, 100);

            var status = score >= 80
                ? SourceHealthStatus.Healthy
                : score >= 50
                    ? SourceHealthStatus.Degraded
                    : SourceHealthStatus.Unhealthy;

            return SourceHealthScore.Of(score, status);
        }
    }
}
