using System;

namespace Formax.Domain.Entities
{
    public class MatchRewardStats
    {
        public int Id { get; set; }

        public int MatchId { get; set; }

        public int Impressions { get; set; }

        public int ClickCount { get; set; }

        public int OpenCount { get; set; }

        public int FollowCount { get; set; }

        public int SkipCount { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}