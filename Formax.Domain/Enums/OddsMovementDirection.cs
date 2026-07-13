namespace Formax.Domain.Enums;

/// <summary>
/// Radar Odds Movement (R.11.1) — direction of an odds change for a match.
/// Deterministic; no AI. Computed from current vs previous odds.
/// </summary>
public enum OddsMovementDirection
{
    Stable = 0,
    Rising = 1,
    Falling = 2
}
