using System;

namespace Formax.Application.Services.Recommendation;

public class RankingInput
{
    public int MatchId { get; set; }

    public double InterestScore { get; set; }
    public double TrendScore { get; set; }
    public double NarrativeBoost { get; set; }
    public double SessionScore { get; set; }

    // 🔥 BANDIT DATA
    public int Impressions { get; set; }
    public int ClickCount { get; set; }
    public int OpenCount { get; set; }
    public int FollowCount { get; set; }
    public int SkipCount { get; set; }

    public double TotalDwellSeconds { get; set; }

    // 🔥 FUTURE NUMERIC EMBEDDING
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int LeagueId { get; set; }

    // 🔥 CURRENT STRING EMBEDDING
    public string HomeTeamName { get; set; } = "";
    public string AwayTeamName { get; set; } = "";
    public string LeagueName { get; set; } = "";
    public double PlayRate { get; set; }
    public double TrendDelta { get; set; }
}