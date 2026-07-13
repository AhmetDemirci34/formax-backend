namespace Formax.Domain.Enums;

/// <summary>
/// Radar Commentary (R.12.2) — how much commentary a match should show, based on signal
/// strength. Low-signal matches stay Hidden; only sufficiently rich matches get Full.
/// </summary>
public enum CommentaryVisibility
{
    Hidden = 0,
    Short = 1,
    Full = 2
}
