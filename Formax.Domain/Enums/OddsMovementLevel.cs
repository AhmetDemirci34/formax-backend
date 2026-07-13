namespace Formax.Domain.Enums;

/// <summary>
/// Radar Odds Movement (R.11.1) — magnitude band of an odds change, from the absolute
/// delta. Deterministic thresholds.
/// </summary>
public enum OddsMovementLevel
{
    /// <summary>|delta| 0.00 – 0.05.</summary>
    Weak = 0,

    /// <summary>|delta| 0.06 – 0.15.</summary>
    Medium = 1,

    /// <summary>|delta| 0.16+.</summary>
    Strong = 2
}
