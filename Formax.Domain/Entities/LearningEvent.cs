using System;
using Formax.Domain.Enums;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// Radar Learning (R.14.1) — a normalized user-behaviour event. All feed/detail/follow
    /// interactions are recorded here in one shape, so later learning can read a single
    /// reliable stream. This sprint only collects; it computes nothing.
    /// </summary>
    public sealed class LearningEvent
    {
        public long Id { get; set; }

        public int UserId { get; set; }
        public int MatchId { get; set; }

        public LearningEventType EventType { get; set; }

        /// <summary>Optional numeric payload, e.g. ViewDurationMs / DetailDurationMs.</summary>
        public double? Value { get; set; }

        /// <summary>Where the event was normalized from (e.g. "swipe", "detail", "follow").</summary>
        public string Source { get; set; } = string.Empty;

        public DateTime OccurredAtUtc { get; set; }
    }
}
