namespace Formax.Domain.Enums;

/// <summary>
/// Radar News Intelligence (R.10.2) — deterministic news classification. One category
/// per item, decided by a fixed keyword-priority order. No AI.
/// </summary>
public enum NewsCategory
{
    Injury = 0,
    Transfer = 1,
    Suspension = 2,
    Coach = 3,
    Lineup = 4,
    MatchPreview = 5,
    MatchResult = 6,
    General = 7
}
