using System;

namespace Formax.Domain.Entities
{
    public class MatchRecommendationStat
    {
        public Guid Id { get; set; }

        public int MatchId { get; set; }

        public int Impressions { get; set; }

        public int Clicks { get; set; }

        public int Dwells { get; set; }

        public int Skips { get; set; }

        public int Follows { get; set; }

        // 🔒 ESKİ (korundu)
        public double RewardScore { get; set; }

        // 🔥 YENİ (son hesaplanan reward)
        public double LastReward { get; set; }

        // 🔥 DERIVED METRICS (DB kolon değil)
        public double CTR =>
            Impressions > 0 ? (double)Clicks / Impressions : 0;

        public double SkipRate =>
            Impressions > 0 ? (double)Skips / Impressions : 0;

        public DateTime UpdatedAt { get; set; }
    }
}