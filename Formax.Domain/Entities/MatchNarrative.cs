using System;

namespace Formax.Domain.Entities
{
    public class MatchNarrative
    {
        public Guid Id { get; set; }

        public int MatchId { get; set; }

        public string Title { get; set; } = "";

        public string Story { get; set; } = "";

        public string Reason { get; set; } = "";

        public double NarrativeScore { get; set; }

        public DateTime GeneratedAt { get; set; }
    }
}