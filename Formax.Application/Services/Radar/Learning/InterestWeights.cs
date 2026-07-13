using System;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.2) — deterministic weight per event type. Engagement events
    /// (follow, dwell, detail) weigh positively; skip/unfollow negatively. Duration values
    /// add a small bounded bonus. No AI, no learning — a fixed table.
    /// </summary>
    public static class InterestWeights
    {
        public static double For(LearningEventType type, double? value)
            => type switch
            {
                LearningEventType.Follow => 5.0,
                LearningEventType.DetailReturn => 3.0 + DurationBonus(value, 5000, 2.0),
                LearningEventType.DetailOpen => 2.0,
                LearningEventType.View => 0.5 + DurationBonus(value, 3000, 1.0),
                LearningEventType.Swipe => -0.5,
                LearningEventType.Unfollow => -4.0,
                _ => 0.0
            };

        private static double DurationBonus(double? ms, double scale, double cap)
            => ms is double v && v > 0 ? Math.Min(cap, v / scale) : 0.0;
    }
}
