namespace Formax.Application.Services.Narrative;

public class NarrativeIntelligenceEngine
{
    public NarrativeResult BuildNarrative(
        string homeTeam,
        string awayTeam,
        double trendScore,
        double interestScore)
    {
        var reason = "Discovery opportunity";
        var tone = "neutral";

        if (trendScore > 0.7)
        {
            reason = "Trending match on Formax";
            tone = "exciting";
        }

        if (interestScore > 0.8)
        {
            reason = "Strong user interest detected";
            tone = "focused";
        }

        var story =
            $"{homeTeam} vs {awayTeam} is drawing attention on the platform.";

        return new NarrativeResult
        {
            Reason = reason,
            Story = story,
            Tone = tone
        };
    }
}

public class NarrativeResult
{
    public string Reason { get; set; } = string.Empty;

    public string Story { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;
}
