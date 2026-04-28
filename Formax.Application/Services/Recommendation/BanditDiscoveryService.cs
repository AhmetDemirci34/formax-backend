using System;
using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.Recommendation
{
    public class BanditDiscoveryService
    {
        private readonly IMatchRewardStatsRepository _statsRepository;

        public BanditDiscoveryService(IMatchRewardStatsRepository statsRepository)
        {
            _statsRepository = statsRepository;
        }

        public async Task<double> CalculateBanditBoost(int matchId)
        {
            var stats = await _statsRepository.GetAsync(matchId);

            if (stats == null || stats.Impressions == 0)
                return 1;

            var reward =
                (stats.ClickCount * 3) +
                (stats.OpenCount * 6) +
                (stats.FollowCount * 8);

            double exploitation = reward / stats.Impressions;

            double exploration =
                Math.Sqrt(Math.Log(stats.Impressions + 1) / stats.Impressions);

            return exploitation + exploration;
        }

        public async Task RegisterImpression(int matchId)
        {
            await _statsRepository.IncrementImpressionAsync(matchId);
        }

        public async Task RegisterClick(int matchId)
        {
            await _statsRepository.IncrementClickAsync(matchId);
        }

        public async Task RegisterOpen(int matchId)
        {
            await _statsRepository.IncrementOpenAsync(matchId);
        }

        public async Task RegisterFollow(int matchId)
        {
            await _statsRepository.IncrementFollowAsync(matchId);
        }
    }
}