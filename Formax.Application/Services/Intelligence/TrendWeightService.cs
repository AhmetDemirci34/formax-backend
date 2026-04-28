using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Services.Intelligence;

public class TrendWeightService
{
    public double CalculateWeight(
        string sessionType,
        string insightLabel,
        double trendScore
    )
    {
        double baseWeight = 1.0;

        // SESSION
        baseWeight *= sessionType switch
        {
            "AGGRESSIVE" => 1.3,
            "CAUTIOUS" => 0.7,
            _ => 1.0
        };

        // INSIGHT
        baseWeight *= insightLabel switch
        {
            "HOT" => 1.2,
            "SAFE" => 0.8,
            _ => 1.0
        };

        // TREND POWER
        if (trendScore > 70)
            baseWeight *= 1.2;
        else if (trendScore < 40)
            baseWeight *= 0.85;

        return Math.Clamp(baseWeight, 0.5, 1.8);
    }
}