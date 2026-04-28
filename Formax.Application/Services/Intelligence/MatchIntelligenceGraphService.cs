using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Services.Intelligence;

public class MatchIntelligenceGraphService
{
    public double CalculateGraphScore(
        double teamStrength,
        double leagueImportance,
        double trendScore,
        double userInterest)
    {
        var score =
            (teamStrength * 0.35) +
            (leagueImportance * 0.20) +
            (trendScore * 0.25) +
            (userInterest * 0.20);

        return score;
    }
}
