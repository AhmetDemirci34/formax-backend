using System;
using Formax.Domain.Enums;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Radar Source Engine (R.8.7) — persisted monitor verdict for one source, refreshed
    /// each scheduler cycle by reading the health snapshot and staging signal. One row
    /// per <see cref="SourceDefinition"/> (shared PK = SourceId).
    ///
    /// Autonomous evaluation only: it classifies state and records the dominant alert.
    /// It does NOT send notifications, switch sources, or touch Intelligence.
    /// </summary>
    public sealed class SourceMonitorSnapshot
    {
        /// <summary>FK + PK → <see cref="SourceDefinition.Id"/>.</summary>
        public int SourceId { get; set; }

        public string SourceKey { get; set; } = string.Empty;

        public SourceMonitorStatus Status { get; set; } = SourceMonitorStatus.Unknown;
        public SourceMonitorAlertType AlertType { get; set; } = SourceMonitorAlertType.None;

        /// <summary>Human-readable explanation of the verdict.</summary>
        public string Reason { get; set; } = string.Empty;

        // ── Evaluated facts (snapshot of the health inputs at eval time) ───────
        public double HealthScoreAtEval { get; set; }
        public int ConsecutiveFailuresAtEval { get; set; }
        public DateTime? LastSuccessAtUtc { get; set; }
        public double MinutesSinceLastSuccess { get; set; }

        public DateTime EvaluatedAtUtc { get; set; }
    }
}
