using System;
using Formax.Domain.Enums;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Radar Source Engine (R.8.6) — persisted health record for one source, updated
    /// each time the scheduler records an execution outcome. One row per
    /// <see cref="SourceDefinition"/> (shared PK = SourceId).
    ///
    /// This is measurement only: it reflects whether the collect→normalize→stage chain
    /// is succeeding. It does not drive failover or alerting (Monitor is a later sprint).
    /// </summary>
    public sealed class SourceHealthSnapshot
    {
        /// <summary>FK + PK → <see cref="SourceDefinition.Id"/>.</summary>
        public int SourceId { get; set; }

        public string SourceKey { get; set; } = string.Empty;

        // ── Raw counters ──────────────────────────────────────────────────────
        public long TotalExecutions { get; set; }
        public long SuccessCount { get; set; }
        public long FailureCount { get; set; }
        public int ConsecutiveFailures { get; set; }

        // ── Timestamps ────────────────────────────────────────────────────────
        public DateTime? LastSuccessAtUtc { get; set; }
        public DateTime? LastFailureAtUtc { get; set; }

        // ── Derived metrics ───────────────────────────────────────────────────
        /// <summary>SuccessCount / TotalExecutions, 0..1.</summary>
        public double SuccessRate { get; set; }

        /// <summary>FailureCount / TotalExecutions, 0..1.</summary>
        public double FailureRate { get; set; }

        /// <summary>Running average successful-or-failed execution time, milliseconds.</summary>
        public double AvgExecutionTimeMs { get; set; }

        /// <summary>Composite health 0..100.</summary>
        public double HealthScore { get; set; }

        public SourceHealthStatus Status { get; set; } = SourceHealthStatus.Unknown;

        public DateTime UpdatedAtUtc { get; set; }
    }
}
