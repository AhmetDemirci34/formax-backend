using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class FeedScoreLog
{
    public int Id { get; set; } // 🔥 EKLE (PRIMARY KEY)

    public int UserId { get; set; }
    public int MatchId { get; set; }

    public double InterestScore { get; set; }
    public double BanditScore { get; set; }
    public double ExplorationScore { get; set; }
    public double AffinityScore { get; set; }
    public double SessionScore { get; set; }
    public double TrendScore { get; set; }
    public double NarrativeScore { get; set; }
    public double DominanceBoost { get; set; }
    public double PersonalBoost { get; set; }
    public double SkipPenalty { get; set; }

    public double FinalScore { get; set; }

    public bool IsExplore { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int RankPosition { get; set; }

    public string DecisionType { get; set; } = ""; // HOT / RELEVANT / DISCOVERY

    public string WeightSnapshot { get; set; } = ""; // JSON
    public double RewardScore { get; set; }
}
