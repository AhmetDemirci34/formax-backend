using System;

namespace Formax.Domain.Entities
{
    public class MatchTrendStat
    {
        public Guid Id { get; set; }

        public int MatchId { get; set; }

        public int Impressions { get; set; }
        public int Clicks { get; set; }
        public int Dwells { get; set; }
        public int Goals { get; set; }

        public double TrendScore { get; set; }

        public DateTime UpdatedAt { get; set; }

        // 🔥 NEW (DERIVED DATA)
        public double PlayRate => Impressions == 0
            ? 0
            : (double)Clicks / Impressions * 100;

        public double Delta => TrendScore;
    }
}