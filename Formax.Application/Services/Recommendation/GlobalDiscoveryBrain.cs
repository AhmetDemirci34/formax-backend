using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Services.Recommendation;

public class GlobalDiscoveryBrain
{
    public double CalculateDiscoveryScore(
        double rankingScore,
        double trendScore,
        double interestScore,
        double graphScore,
        double explorationBoost)
    {
        var finalScore =
            (rankingScore * 0.35) +
            (trendScore * 0.20) +
            (interestScore * 0.20) +
            (graphScore * 0.15) +
            (explorationBoost * 0.10);

        return finalScore;
    }
}
