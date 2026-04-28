using System;

namespace Formax.Domain.Entities
{
    public class MatchFeedState
    {
        public Guid Id { get; set; }

        public int MatchId { get; set; }

        public string State { get; set; } = "new";

        public int Impressions { get; set; }

        public int Clicks { get; set; }

        public double Score { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
