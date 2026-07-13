namespace Formax.Domain.Enums;

/// <summary>
/// Radar News Intelligence (R.10.3) — banded news impact derived from the 0-100
/// NewsImpactScore. Deterministic; no AI.
/// </summary>
public enum NewsImpactLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
