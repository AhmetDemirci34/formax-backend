namespace Formax.Domain.Enums;

/// <summary>
/// Radar Source Engine (R.8) — whether a source is a permanent part of the
/// engine or a temporary bridge scheduled for Replace-Then-Remove.
/// Sources marked Temporary surface automatically on the R.9 decision list.
/// </summary>
public enum SourceLifecycle
{
    Permanent = 0,

    /// <summary>Temporary bridge (e.g. TheSportsDB). Removed once a permanent
    /// source joins its FailoverGroup.</summary>
    Temporary = 1
}
