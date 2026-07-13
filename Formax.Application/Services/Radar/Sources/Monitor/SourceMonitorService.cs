using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Sources.Monitor
{
    /// <summary>
    /// Radar Source Engine (R.8.7) — default monitor. Reads the health snapshot and the
    /// staging signal, runs the five deterministic checks, and records a verdict
    /// (Healthy / Warning / Critical) with the dominant alert. Highest-severity check
    /// wins. No side effects beyond persisting the monitor snapshot.
    /// </summary>
    public sealed class SourceMonitorService : ISourceMonitorService
    {
        // ── Thresholds (deterministic) ────────────────────────────────────────
        private const int CriticalConsecutiveFailures = 5;
        private const int WarningConsecutiveFailures = 3;
        private const double CriticalHealthScore = 50;
        private const double WarningHealthScore = 75;
        private const double NoRecentSuccessMinutes = 30;   // check 2
        private const double DataFlowStoppedMinutes = 60;   // check 1
        private const long NoStagingExecThreshold = 5;      // check 5 gate

        private readonly ISourceHealthRepository _health;
        private readonly IStagingRepository _staging;
        private readonly ISourceMonitorRepository _monitor;
        private readonly ILogger<SourceMonitorService> _logger;

        public SourceMonitorService(
            ISourceHealthRepository health,
            IStagingRepository staging,
            ISourceMonitorRepository monitor,
            ILogger<SourceMonitorService> logger)
        {
            _health = health;
            _staging = staging;
            _monitor = monitor;
            _logger = logger;
        }

        public async Task<MonitorEvaluationResult> EvaluateAsync(
            SourceDefinition definition, DateTime nowUtc, CancellationToken ct = default)
        {
            try
            {
                var health = await _health.GetBySourceIdAsync(definition.Id, ct);
                if (health is null || health.TotalExecutions == 0)
                    return MonitorEvaluationResult.NoHealthData(definition.Id, definition.SourceKey);

                var minutesSinceSuccess = health.LastSuccessAtUtc.HasValue
                    ? (nowUtc - health.LastSuccessAtUtc.Value).TotalMinutes
                    : double.MaxValue;

                var pendingStaging = await _staging.CountPendingAsync(ct);

                var (status, alert, reason) = Evaluate(health, minutesSinceSuccess, pendingStaging);

                var existing = await _monitor.GetBySourceIdAsync(definition.Id, ct);
                var isNew = existing is null;

                var snapshot = existing ?? new SourceMonitorSnapshot { SourceId = definition.Id };
                snapshot.SourceKey = definition.SourceKey;
                snapshot.Status = status;
                snapshot.AlertType = alert;
                snapshot.Reason = reason;
                snapshot.HealthScoreAtEval = health.HealthScore;
                snapshot.ConsecutiveFailuresAtEval = health.ConsecutiveFailures;
                snapshot.LastSuccessAtUtc = health.LastSuccessAtUtc;
                snapshot.MinutesSinceLastSuccess =
                    minutesSinceSuccess == double.MaxValue ? -1 : minutesSinceSuccess;
                snapshot.EvaluatedAtUtc = nowUtc;

                await _monitor.UpsertAsync(snapshot, ct);
                await _monitor.SaveChangesAsync(ct);

                return new MonitorEvaluationResult
                {
                    SourceId = definition.Id,
                    SourceKey = definition.SourceKey,
                    Status = status,
                    AlertType = alert,
                    Reason = reason,
                    Outcome = isNew ? MonitorEvaluationOutcome.Created : MonitorEvaluationOutcome.Updated
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RADAR MONITOR] {Key} evaluation failed.", definition.SourceKey);
                return MonitorEvaluationResult.Failed(definition.Id, definition.SourceKey, ex.Message);
            }
        }

        public Task<SourceMonitorSnapshot?> GetAsync(int sourceId, CancellationToken ct = default)
            => _monitor.GetBySourceIdAsync(sourceId, ct);

        /// <summary>
        /// Five checks, highest severity wins:
        ///   Critical: data-flow stopped (1) · health critical (3) · consecutive≥5 (4)
        ///   Warning:  no recent success (2) · consecutive≥3 (4) · low health · no staging (5)
        /// </summary>
        private static (SourceMonitorStatus, SourceMonitorAlertType, string) Evaluate(
            SourceHealthSnapshot h, double minutesSinceSuccess, int pendingStaging)
        {
            // ── Critical ──────────────────────────────────────────────────────
            if (h.LastSuccessAtUtc.HasValue && minutesSinceSuccess >= DataFlowStoppedMinutes)
                return (SourceMonitorStatus.Critical, SourceMonitorAlertType.DataFlowStopped,
                    $"no success for {minutesSinceSuccess:F0}m (≥{DataFlowStoppedMinutes:F0}m)");

            if (!h.LastSuccessAtUtc.HasValue && h.TotalExecutions > 0)
                return (SourceMonitorStatus.Critical, SourceMonitorAlertType.DataFlowStopped,
                    "no success ever recorded despite executions");

            if (h.HealthScore < CriticalHealthScore)
                return (SourceMonitorStatus.Critical, SourceMonitorAlertType.HealthCritical,
                    $"health score {h.HealthScore:F0} < {CriticalHealthScore:F0}");

            if (h.ConsecutiveFailures >= CriticalConsecutiveFailures)
                return (SourceMonitorStatus.Critical, SourceMonitorAlertType.ConsecutiveFailures,
                    $"consecutive failures {h.ConsecutiveFailures} ≥ {CriticalConsecutiveFailures}");

            // ── Warning ───────────────────────────────────────────────────────
            if (h.ConsecutiveFailures >= WarningConsecutiveFailures)
                return (SourceMonitorStatus.Warning, SourceMonitorAlertType.ConsecutiveFailures,
                    $"consecutive failures {h.ConsecutiveFailures} ≥ {WarningConsecutiveFailures}");

            if (minutesSinceSuccess >= NoRecentSuccessMinutes && minutesSinceSuccess != double.MaxValue)
                return (SourceMonitorStatus.Warning, SourceMonitorAlertType.NoRecentSuccess,
                    $"last success {minutesSinceSuccess:F0}m ago (≥{NoRecentSuccessMinutes:F0}m)");

            if (h.HealthScore < WarningHealthScore)
                return (SourceMonitorStatus.Warning, SourceMonitorAlertType.HealthCritical,
                    $"health score {h.HealthScore:F0} < {WarningHealthScore:F0}");

            // Check 5 — gated so it doesn't fire before real collectors exist.
            if (h.TotalExecutions >= NoStagingExecThreshold && pendingStaging == 0)
                return (SourceMonitorStatus.Warning, SourceMonitorAlertType.NoStagingData,
                    $"no staged data after {h.TotalExecutions} executions");

            // ── Healthy ───────────────────────────────────────────────────────
            return (SourceMonitorStatus.Healthy, SourceMonitorAlertType.None, "healthy");
        }
    }
}
