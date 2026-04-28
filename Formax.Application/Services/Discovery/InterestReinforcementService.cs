namespace Formax.Application.Services.Discovery;

public class InterestReinforcementService
{
    public double CalculateBoost(string eventType, double dwellSeconds)
    {
        double boost = 0;

        if (eventType == "click")
            boost = 2;

        if (eventType == "follow")
            boost = 5;

        if (eventType == "dwell")
            boost = dwellSeconds / 10;

        return boost;
    }
}
