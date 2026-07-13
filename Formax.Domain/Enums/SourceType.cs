namespace Formax.Domain.Enums;

/// <summary>
/// Radar Source Engine (R.8) — physical collection mechanism of a source.
/// Determines which Collector implementation handles the source.
/// Registry-only (R.8.1): collectors are implemented in later sprints.
/// </summary>
public enum SourceType
{
    /// <summary>Internal FORMAX data (UserActions, follows, feed interactions).</summary>
    Internal = 0,

    /// <summary>RSS / Atom news feed (NABIZ family).</summary>
    Rss = 1,

    /// <summary>Temporary bridge over an existing external provider (e.g. TheSportsDB).
    /// Lifecycle is expected to be Temporary; replaced under Replace-Then-Remove.</summary>
    Bridge = 2,

    /// <summary>HTML scrape source. Defined but kept inactive in R.8 (fragile / legal risk).</summary>
    Html = 3
}
