using System;

namespace Formax.Domain.Entities
{
    public class MatchLiveEvent
    {
        public Guid Id { get; set; }

        public int MatchId { get; set; }

        public string EventType { get; set; } = "";

        public int Minute { get; set; }

        public string? Team { get; set; }

        public string? Player { get; set; }

        /// <summary>Free-text detail from the provider, e.g. "Normal Goal", "Yellow Card".</summary>
        public string? Detail { get; set; }

        public double ImpactScore { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
