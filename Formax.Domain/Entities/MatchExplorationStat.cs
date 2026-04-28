using System;

namespace Formax.Domain.Entities
{
    public class MatchExplorationStat
    {
        public Guid Id { get; set; }

        public int MatchId { get; set; }

        public int ExplorationImpressions { get; set; }

        public int ExplorationClicks { get; set; }

        public double ExplorationScore { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
