using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Monitor
{
    /// <summary>
    /// Radar Source Engine (R.8.7) — result of evaluating a source's monitor state.
    /// </summary>
    public sealed class MonitorEvaluationResult
    {
        public int SourceId { get; init; }
        public string SourceKey { get; init; } = string.Empty;

        public SourceMonitorStatus Status { get; init; }
        public SourceMonitorAlertType AlertType { get; init; }
        public string Reason { get; init; } = string.Empty;

        public MonitorEvaluationOutcome Outcome { get; init; }
        public string? Error { get; init; }

        public static MonitorEvaluationResult NoHealthData(int sourceId, string sourceKey)
            => new()
            {
                SourceId = sourceId,
                SourceKey = sourceKey,
                Status = SourceMonitorStatus.Unknown,
                AlertType = SourceMonitorAlertType.None,
                Reason = "no health data",
                Outcome = MonitorEvaluationOutcome.NoHealthData
            };

        public static MonitorEvaluationResult Failed(int sourceId, string sourceKey, string error)
            => new()
            {
                SourceId = sourceId,
                SourceKey = sourceKey,
                Status = SourceMonitorStatus.Unknown,
                AlertType = SourceMonitorAlertType.None,
                Reason = "evaluation failed",
                Outcome = MonitorEvaluationOutcome.Failed,
                Error = error
            };
    }
}
