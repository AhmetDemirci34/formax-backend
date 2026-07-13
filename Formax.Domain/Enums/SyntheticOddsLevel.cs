namespace Formax.Domain.Enums;

/// <summary>
/// Radar Odds Movement (R.11.3) — banded synthetic interest level derived from Radar's
/// internal signals (interest + news impact + importance). Answers "which matches are
/// drawing rising interest?", not a real odds value.
/// </summary>
public enum SyntheticOddsLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
