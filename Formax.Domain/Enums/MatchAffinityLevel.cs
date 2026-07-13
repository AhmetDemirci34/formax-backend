namespace Formax.Domain.Enums;

/// <summary>
/// Radar Learning (R.14.3) — banded match affinity level derived from the 0-100
/// AffinityScore. Deterministic; no AI.
/// </summary>
public enum MatchAffinityLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
