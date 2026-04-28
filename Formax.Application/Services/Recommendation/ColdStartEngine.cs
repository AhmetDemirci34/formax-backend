using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.Services.Recommendation
{
    public class ColdStartEngine
    {
        // 🔥 Cold threshold
        private const double USER_TREND_THRESHOLD = 0.4;
        private const double WEAK_RATIO_THRESHOLD = 0.7;

        public bool IsColdStart(List<RecommendationCardDto> items)
        {
            if (items == null || items.Count == 0)
                return true;

            var avgUserTrend = items.Average(x => x.UserTrendScore);
            var weakSignals = items.Count(x => x.UserTrendScore < USER_TREND_THRESHOLD);
            var ratio = (double)weakSignals / items.Count;

            // 🔥 hybrid kontrol
            return avgUserTrend < USER_TREND_THRESHOLD || ratio > WEAK_RATIO_THRESHOLD;
        }

        public List<RecommendationCardDto> Build(List<RecommendationCardDto> items)
        {
            if (items == null || items.Count == 0)
                return new List<RecommendationCardDto>();

            foreach (var item in items)
            {
                var global = item.GlobalTrendScore;
                var market = item.ExternalMomentum;

                // 🔥 user sinyalini ignore ediyoruz
                var score =
                    (global * 0.5) +
                    (market * 0.3);

                // 🔥 NON-LINEAR BOOST (AI hissi)
                score = Math.Tanh(score * 2);

                item.Score = score * 100;
            }

            // 🔥 exploration + varyasyon (ölü feed engelle)
            var rnd = new Random();

            foreach (var item in items)
            {
                var variation = rnd.NextDouble() * 20; // 0–20
                item.Score += variation;

                item.Score += rnd.NextDouble() * 3; // micro jitter

                // 🔥 clamp
                if (item.Score > 100)
                    item.Score = 100;

                if (item.Score < 0)
                    item.Score = 0;

                item.Score *= 0.9;

            }

            var maxScore = items.Max(x => x.Score);

            foreach (var item in items)
            {
                if (item.Score == maxScore)
                    item.Score += 5;
            }

            return items
                .OrderByDescending(x => x.Score)
                .ToList();
        }
    }
}