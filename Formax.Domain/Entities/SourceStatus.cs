using System;
using Formax.Domain.Enums;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Radar Source Engine (R.8.1) — mutable runtime state and health metrics for a
    /// single source. One row per <see cref="SourceDefinition"/> (shared PK = SourceId).
    ///
    /// Definition holds config (git/appsettings-authored); status holds state
    /// (engine-written). This sprint persists the shape and exposes upsert; the
    /// Health / Monitor / autonomous-decision engines that drive these fields arrive
    /// in later R.8.x sprints.
    /// </summary>
    public sealed class SourceStatus
    {
        /// <summary>FK + PK → <see cref="SourceDefinition.Id"/>.</summary>
        public int SourceId { get; set; }

        /// <summary>Runtime usability, managed by the engine (circuit/failover).
        /// Distinct from operator-controlled <see cref="SourceDefinition.Enabled"/>.</summary>
        public bool Active { get; set; } = true;

        // ── Outcome timestamps ────────────────────────────────────────────────
        public DateTime? LastSuccessUtc { get; set; }
        public DateTime? LastFailureUtc { get; set; }

        /// <summary>Last time the source actually delivered NEW content.
        /// Distinct from LastSuccessUtc — guards against "200 OK but stale" sources.</summary>
        public DateTime? DataFreshnessAtUtc { get; set; }

        /// <summary>Last normalize failure detail (fetch ok but payload unparseable).</summary>
        public string? LastNormalizeError { get; set; }

        // ── Counters ──────────────────────────────────────────────────────────
        public long SuccessCount { get; set; }
        public long ErrorCount { get; set; }
        public long TimeoutCount { get; set; }

        /// <summary>Consecutive failures — input to the circuit-open decision.</summary>
        public int ConsecutiveFailures { get; set; }

        // ── Derived metrics (0..1 unless noted) ───────────────────────────────
        public double SuccessRate { get; set; }
        public double DuplicateRate { get; set; }

        /// <summary>Average successful fetch duration, milliseconds.</summary>
        public double AvgDurationMs { get; set; }

        /// <summary>Composite source health 0..100. Internal use only.</summary>
        public double HealthScore { get; set; }

        // ── Volume tracking (missing-data detection) ──────────────────────────
        public int LastVolume { get; set; }
        public int ExpectedVolume { get; set; }

        // ── Circuit breaker ───────────────────────────────────────────────────
        public SourceCircuitState CircuitState { get; set; } = SourceCircuitState.Closed;
        public DateTime? CircuitOpenedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }
}
