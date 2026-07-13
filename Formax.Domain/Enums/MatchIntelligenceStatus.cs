namespace Formax.Domain.Enums;

/// <summary>
/// Radar Match Intelligence (R.9.1) — state of a match's intelligence snapshot.
/// </summary>
public enum MatchIntelligenceStatus
{
    /// <summary>Could not be built (match/data missing).</summary>
    Unknown = 0,

    /// <summary>Built, but no signals fired.</summary>
    NoSignal = 1,

    /// <summary>Built with one or more signals.</summary>
    Active = 2
}
