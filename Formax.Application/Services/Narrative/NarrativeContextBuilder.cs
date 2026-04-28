namespace Formax.Application.Services.Narrative;

public class NarrativeContextBuilder
{
    public NarrativeContext BuildContext(
        string homeTeam,
        string awayTeam,
        double homeForm,
        double awayForm,
        double trendScore,
        double interestScore)
    {
        var importance = "normal";

        if (trendScore > 0.7)
            importance = "trending";

        if (interestScore > 0.8)
            importance = "high-interest";

        return new NarrativeContext
        {
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            HomeForm = homeForm,
            AwayForm = awayForm,
            TrendScore = trendScore,
            InterestScore = interestScore,
            Importance = importance
        };
    }
}

public class NarrativeContext
{
    public string HomeTeam { get; set; } = string.Empty;

    public string AwayTeam { get; set; } = string.Empty;

    public double HomeForm { get; set; }

    public double AwayForm { get; set; }

    public double TrendScore { get; set; }

    public double InterestScore { get; set; }

    public string Importance { get; set; } = string.Empty;
}