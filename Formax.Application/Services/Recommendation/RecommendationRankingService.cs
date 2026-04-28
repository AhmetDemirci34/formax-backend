using System;

namespace Formax.Application.Services.Recommendation
{
    public class RecommendationRankingService
    {
        public double CalculateScore(
            int radarScore,
            int teamInterest,
            int leagueInterest,
            int contentInterest,
            int momentum,
            int heat,
            int baseline,
            int timeProximity,
            double banditScore,
            double trendScore,
            double narrativeScore)
        {
            double rawScore =
                (radarScore * 0.20) +
                (teamInterest * 0.08) +
                (leagueInterest * 0.06) +
                (contentInterest * 0.06) +
                (momentum * 0.11) +
                (heat * 0.10) +
                (baseline * 0.07) +
                (timeProximity * 0.09) +
                (banditScore * 0.11) +
                (trendScore * 0.07) +
                (narrativeScore * 0.05);

            return Normalize(rawScore);
        }

        private double Normalize(double score)
        {
            if (score < 0)
                score = 0;

            if (score > 100)
                score = 100;

            return score;
        }
    }
}