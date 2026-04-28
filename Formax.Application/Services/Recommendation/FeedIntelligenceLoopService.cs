namespace Formax.Application.Services.Recommendation;

public class FeedIntelligenceLoopService
{
    public double ProcessInteraction(
        string eventType,
        double dwellSeconds)
    {
        double reward = 0;

        switch (eventType)
        {
            case "click":
                reward = 3;
                break;

            case "follow":
                reward = 8;
                break;

            case "skip":
                reward = -2;
                break;

            case "dwell":
                reward = dwellSeconds / 5;
                break;

            case "impression":
                reward = 0.1;
                break;
        }

        return reward;
    }
}