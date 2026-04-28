using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.Services.Recommendation
{
    public class RankingEngineV2
    {
        private const int MAX_SAME_TEAM = 2;
        private const double EXPLORATION_RATE = 0.15;

        public List<RecommendationCardDto> Rank(int userId, List<RecommendationCardDto> input)
        {
            if (input == null || input.Count == 0)
                return new List<RecommendationCardDto>();

            // 🔥 COLD START
            var coldStart = new ColdStartEngine();

            if (coldStart.IsColdStart(input))
                return coldStart.Build(input);

            // 🔥 FILTER
            input = input
                .Where(x => x.Score >= 20)
                .ToList();

            if (input.Count == 0)
                return new List<RecommendationCardDto>();

            // 🔥 NORMALIZE
            var normalized = Normalize(input);

            normalized = normalized
                .Where(x => x.Score >= 5)
                .ToList();

            if (normalized.Count == 0)
                return new List<RecommendationCardDto>();

            // 🔥 BOOST
            var boosted = ApplyBoost(normalized);

            // 🔥 SORT
            var sorted = boosted
                .OrderByDescending(x => x.Score)
                .ToList();

            // 🔥 DIVERSITY
            var diversified = ApplyTeamLimit(sorted);

            // 🔥 EXPLORATION
            return ApplyExploration(diversified);
        }

        // --------------------------

        private List<RecommendationCardDto> Normalize(List<RecommendationCardDto> list)
        {
            var max = list.Max(x => x.Score);
            var min = list.Min(x => x.Score);

            if (max == min)
                return list;

            foreach (var item in list)
            {
                var normalized = (item.Score - min) / (max - min) * 100.0;

                if (double.IsNaN(normalized) || double.IsInfinity(normalized))
                    normalized = 0;

                item.Score = normalized;
            }

            return list;
        }

        private List<RecommendationCardDto> ApplyBoost(List<RecommendationCardDto> list)
        {
            foreach (var item in list)
            {
                double boost = 0;

                if (item.GlobalTrendScore > 0.7)
                    boost += 5;

                if (item.ExternalMomentum > 0.7)
                    boost += 5;

                if (item.UserTrendScore > 0.7)
                    boost += 5;

                // 🔥 AGGRESSIVE GLOBAL BOOST
                boost += Math.Pow(item.CrossUserScore, 2) * 20;

                if (item.Score > 80)
                    boost += 3;

                item.Score += boost;

                if (item.Score > 100)
                    item.Score = 100;

                if (item.Score < 0)
                    item.Score = 0;
            }

            return list;
        }

        private List<RecommendationCardDto> ApplyTeamLimit(List<RecommendationCardDto> list)
        {
            var result = new List<RecommendationCardDto>();
            var overflow = new List<RecommendationCardDto>();

            var teamCount = new Dictionary<string, int>();
            var teamLastIndex = new Dictionary<string, int>();

            foreach (var item in list)
            {
                var keyA = item.TeamA ?? "";
                var keyB = item.TeamB ?? "";

                int countA = teamCount.ContainsKey(keyA) ? teamCount[keyA] : 0;
                int countB = teamCount.ContainsKey(keyB) ? teamCount[keyB] : 0;

                if (countA >= MAX_SAME_TEAM || countB >= MAX_SAME_TEAM)
                {
                    overflow.Add(item);
                    continue;
                }

                bool tooClose =
                    (teamLastIndex.ContainsKey(keyA) && result.Count - teamLastIndex[keyA] < 3) ||
                    (teamLastIndex.ContainsKey(keyB) && result.Count - teamLastIndex[keyB] < 3);

                if (tooClose)
                {
                    overflow.Add(item);
                    continue;
                }

                result.Add(item);

                teamCount[keyA] = countA + 1;
                teamCount[keyB] = countB + 1;

                teamLastIndex[keyA] = result.Count - 1;
                teamLastIndex[keyB] = result.Count - 1;
            }

            foreach (var item in overflow)
            {
                result.Add(item);
            }

            return result;
        }

        private List<RecommendationCardDto> ApplyExploration(List<RecommendationCardDto> list)
        {
            var rnd = new Random();

            int explorationCount = (int)(list.Count * EXPLORATION_RATE);

            var shuffled = list
                .OrderBy(x => rnd.Next())
                .ToList();

            var explorationItems = shuffled
                .Take(explorationCount)
                .ToList();

            var remaining = list
                .Except(explorationItems)
                .OrderByDescending(x => x.Score)
                .ToList();

            foreach (var item in explorationItems)
            {
                int index = rnd.Next(0, Math.Min(5, remaining.Count));
                remaining.Insert(index, item);
            }

            return remaining;
        }
    }
}