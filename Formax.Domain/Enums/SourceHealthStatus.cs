namespace Formax.Domain.Enums;

/// <summary>
/// Radar Source Engine (R.8.6) — coarse health classification derived from a source's
/// HealthScore and recent execution outcomes. Used by operators/diagnostics; the
/// Monitor engine (later sprint) consumes this, but Monitor is NOT built here.
/// </summary>
public enum SourceHealthStatus
{
    /// <summary>No executions recorded yet.</summary>
    Unknown = 0,

    /// <summary>Performing well (high success rate, no recent failure streak).</summary>
    Healthy = 1,

    /// <summary>Mixed signals — degraded but still usable.</summary>
    Degraded = 2,

    /// <summary>Failing — low success rate or sustained consecutive failures.</summary>
    Unhealthy = 3
}
