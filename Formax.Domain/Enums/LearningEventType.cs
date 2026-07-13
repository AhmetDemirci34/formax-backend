namespace Formax.Domain.Enums;

/// <summary>
/// Radar Learning (R.14.1) — normalized user-behaviour event type. A single vocabulary
/// over the existing FeedInteractionEvent / UserAction signals. Foundation only — no
/// scoring, no profile, no ranking effect.
/// </summary>
public enum LearningEventType
{
    Swipe = 0,
    View = 1,
    DetailOpen = 2,
    DetailReturn = 3,
    Follow = 4,
    Unfollow = 5
}
