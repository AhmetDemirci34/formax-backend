namespace Formax.Domain.Enums;

/// <summary>
/// Radar Match Intelligence (R.9.6) — banded importance level derived from the
/// 0-100 ImportanceScore.
/// </summary>
public enum MatchImportanceLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
