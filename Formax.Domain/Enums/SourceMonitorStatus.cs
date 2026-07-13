namespace Formax.Domain.Enums;

/// <summary>
/// Radar Source Engine (R.8.7) — overall monitor verdict for a source, derived from
/// its health snapshot and data-flow checks. Operator/diagnostics signal only; this
/// sprint takes no action (no notifications, no failover).
/// </summary>
public enum SourceMonitorStatus
{
    /// <summary>No executions yet — nothing to judge.</summary>
    Unknown = 0,

    Healthy = 1,

    /// <summary>Degraded signals — worth attention, not yet failing.</summary>
    Warning = 2,

    /// <summary>Failing — needs intervention.</summary>
    Critical = 3
}
