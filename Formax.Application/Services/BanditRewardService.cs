using System;
using Formax.Application.Services.Recommendation;

namespace Formax.Application.Services
{
    public interface IBanditRewardService
    {
        double CalculateReward(
            string eventType,
            double ctr = 0,
            double skipRate = 0,
            bool hasClick = false,
            bool hasFollow = false);
    }

    public class BanditRewardService : IBanditRewardService
    {
        private readonly RewardCalculator _rewardCalculator;

        public BanditRewardService(RewardCalculator rewardCalculator)
        {
            _rewardCalculator = rewardCalculator;
        }

        public double CalculateReward(
            string eventType,
            double ctr = 0,
            double skipRate = 0,
            bool hasClick = false,
            bool hasFollow = false)
        {
            // 🔒 normalize
            eventType = (eventType ?? "").ToLowerInvariant();

            // 🔥 BASE EVENT (fallback)
            double baseReward = eventType switch
            {
                "view" => 0.0,
                "impression" => 0.0,
                "open" => 1.0,
                "click" => 1.0,
                "follow" => 2.0,
                "skip" => -0.3,
                _ => 0.0
            };

            // 🔥 ADVANCED REWARD (PHASE 7.4)
            double advanced = _rewardCalculator.CalculateFinalScore(
                ctr,
                skipRate,
                hasClick,
                hasFollow);

            // 🔥 FINAL
            return baseReward + advanced;
        }
    }
}