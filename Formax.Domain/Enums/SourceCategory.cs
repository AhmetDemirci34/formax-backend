namespace Formax.Domain.Enums;

/// <summary>
/// Radar Source Engine (R.8) — the kind of data a source provides.
/// Drives scheduling cadence and which Intelligence layer (R.9+) consumes it.
/// </summary>
public enum SourceCategory
{
    /// <summary>Fixtures, results, match schedule.</summary>
    Match = 0,

    /// <summary>News, injuries, squad signals (RSS).</summary>
    News = 1,

    /// <summary>User behaviour signals (oynanma, follow, interactions).</summary>
    Behavior = 2,

    /// <summary>Derived odds / market movement (computed from internal behaviour).</summary>
    Odds = 3
}
