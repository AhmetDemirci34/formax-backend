using System;

namespace Formax.Application.Services.Recommendation
{
    public class DiscoveryEngineService
    {
        private static readonly Random _random = new Random();

        public double CalculateDiscoveryScore(
            double radarScore,
            double interestScore,
            double rewardScore,
            double banditScore)
        {
            // 🔹 1. Random exploration (küçük jitter)
            double exploration = _random.NextDouble() * 2;

            // 🔹 2. Hidden gem boost (yüksek radar + düşük bandit)
            double hiddenGemBoost = 0;
            if (banditScore < 0.2 && radarScore > 70)
            {
                hiddenGemBoost = 15;
            }

            // 🔹 3. Cold start boost (hiç etkileşim yok)
            double coldStartBoost = 0;
            if (rewardScore == 0 && banditScore <= 0.1)
            {
                coldStartBoost = 10;
            }

            // 🔹 4. Exploration bias (ilgi düşükse keşfe zorla)
            double explorationBias = 0;
            if (interestScore < 30)
            {
                explorationBias = 5;
            }

            // 🔹 FINAL SCORE
            double score =
                (radarScore * 0.25) +
                (interestScore * 0.30) +
                (rewardScore * 0.20) +
                (banditScore * 0.15) +
                exploration +
                hiddenGemBoost +
                coldStartBoost +
                explorationBias;

            return Math.Round(score, 2);
        }
    }
}