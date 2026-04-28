using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Services.Recommendation;

public class GlobalRankingBrain
{
    public double CalculateFinalScore(
        double banditScore,
        double trendScore,
        double interestScore,
        double explorationBoost,
        double narrativeBoost)
    {
        var score =
            (banditScore * 0.40) +
            (trendScore * 0.20) +
            (interestScore * 0.25) +
            (explorationBoost * 0.10) +
            (narrativeBoost * 0.05);

        return score;
    }
}