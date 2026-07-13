namespace Formax.Domain.Enums;

/// <summary>
/// Radar Source Engine (R.8.7) — the dominant reason behind a monitor verdict. Maps
/// to the five monitor checks. <see cref="None"/> when healthy.
/// </summary>
public enum SourceMonitorAlertType
{
    None = 0,

    /// <summary>Check 1 — data flow appears stopped (no success for a long window).</summary>
    DataFlowStopped = 1,

    /// <summary>Check 2 — source has not produced a success recently.</summary>
    NoRecentSuccess = 2,

    /// <summary>Check 3 — HealthScore dropped to a critical level.</summary>
    HealthCritical = 3,

    /// <summary>Check 4 — consecutive failures threshold exceeded.</summary>
    ConsecutiveFailures = 4,

    /// <summary>Check 5 — no data reaching staging despite executions.</summary>
    NoStagingData = 5
}
