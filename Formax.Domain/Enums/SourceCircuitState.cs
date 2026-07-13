namespace Formax.Domain.Enums;

/// <summary>
/// Radar Source Engine (R.8) — circuit-breaker state of a source at runtime.
/// Managed by the autonomous decision layer (later sprints). Stored on
/// SourceStatus. Registry-only (R.8.1) persists the field; transitions are
/// driven by the Health / Monitor engines in R.8.x.
/// </summary>
public enum SourceCircuitState
{
    /// <summary>Healthy — source is used normally.</summary>
    Closed = 0,

    /// <summary>Tripped — source is skipped, failover takes over.</summary>
    Open = 1,

    /// <summary>Probation — a single trial call is allowed to test recovery.</summary>
    HalfOpen = 2
}
