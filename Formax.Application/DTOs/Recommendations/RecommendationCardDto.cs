using Formax.Application.DTOs.Teams;
using Formax.Engine.Core.ExternalTrends;

namespace Formax.Application.DTOs.Recommendations;

public class RecommendationCardDto
{
    public int MatchId { get; set; }

    public TeamDto HomeTeam { get; set; } = new();
    public TeamDto AwayTeam { get; set; } = new();

    public string TeamA { get; set; } = "";
    public string TeamB { get; set; } = "";

    public double Score { get; set; }
    public double RecommendationScore { get; set; }

    public double ConfidenceScore { get; set; }
    public string ConfidenceLabel { get; set; } = "";

    public string CardType { get; set; } = "USER";
    public string PersonalReason { get; set; } = "";

    public TrendDto Trend { get; set; } = new();
    public ExternalDto External { get; set; } = new();

    public ExternalTrendDto? ExternalTrend { get; set; }

    public string InsightLabel { get; set; } = "";
    public string InsightReason { get; set; } = "";
    public int Priority { get; set; }

    public double TrendWeight { get; set; }
    public double TrendImpact { get; set; }

    public double MarketTrendScore { get; set; } = 0;
    public double UserTrendScore { get; set; } = 0;
    public double GlobalTrendScore { get; set; } = 0;

    // 🔥 NEW (Phase 7.5)
    public double ExternalMomentum { get; set; } = 0;

    public string Highlight { get; set; } = "";
    public string AiComment { get; set; } = "";
    public string AiSummary { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public double CrossUserScore { get; set; }
    public double MomentumScore { get; set; }
    public double SpikeScore { get; set; }
    public double DirectionScore { get; set; }
}