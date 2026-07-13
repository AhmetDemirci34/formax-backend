namespace Formax.Domain.Enums;

/// <summary>
/// Radar Commentary (R.12.1) — tone band of a deterministic match commentary, derived
/// from the match's importance/signal strength. No AI.
/// </summary>
public enum CommentaryTone
{
    Neutral = 0,
    Attention = 1,
    Important = 2,
    Critical = 3
}
