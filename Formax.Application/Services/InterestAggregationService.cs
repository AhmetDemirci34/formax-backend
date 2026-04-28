using System;
using System.Threading.Tasks;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.Interest
{
    public sealed class InterestAggregationService : IInterestAggregationService
    {
        private readonly IMatchInterestReadRepository _matchInterestReadRepository;
        private readonly IUserInterestScoreRepository _userInterestScoreRepository;
        private readonly IMatchRewardRepository _matchRewardRepository;

        public InterestAggregationService(
            IMatchInterestReadRepository matchInterestReadRepository,
            IUserInterestScoreRepository userInterestScoreRepository,
            IMatchRewardRepository matchRewardRepository)
        {
            _matchInterestReadRepository = matchInterestReadRepository;
            _userInterestScoreRepository = userInterestScoreRepository;
            _matchRewardRepository = matchRewardRepository;
        }

        public async Task AggregateMatchEventAsync(
            int userId,
            int matchId,
            int weight)
        {
            if (userId <= 0)
                throw new InvalidOperationException("Invalid user id.");

            if (matchId <= 0)
                throw new InvalidOperationException("Invalid match id.");

            if (weight <= 0)
                return;

            var match = await _matchInterestReadRepository.GetByIdAsync(matchId);

            if (match == null)
                throw new InvalidOperationException("Match not found for interest aggregation.");

            var utcNow = DateTime.UtcNow;

            // 🔹 USER INTEREST (team / league öğrenme)
            if (!string.IsNullOrWhiteSpace(match.HomeTeamName))
            {
                await _userInterestScoreRepository.UpsertAsync(
                    userId,
                    "Team",
                    match.HomeTeamName.Trim(),
                    weight,
                    utcNow);
            }

            if (!string.IsNullOrWhiteSpace(match.AwayTeamName))
            {
                await _userInterestScoreRepository.UpsertAsync(
                    userId,
                    "Team",
                    match.AwayTeamName.Trim(),
                    weight,
                    utcNow);
            }

            if (!string.IsNullOrWhiteSpace(match.LeagueName))
            {
                await _userInterestScoreRepository.UpsertAsync(
                    userId,
                    "League",
                    match.LeagueName.Trim(),
                    weight,
                    utcNow);
            }

            // 🔥 CRITICAL: MATCH REWARD (bandit learning için)
            await _matchRewardRepository.UpdateAsync(matchId, weight);
        }

        public async Task AggregateTeamFollowAsync(
            int userId,
            int teamId,
            int weight)
        {
            if (userId <= 0)
                throw new InvalidOperationException("Invalid user id.");

            if (teamId <= 0)
                throw new InvalidOperationException("Invalid team id.");

            if (weight <= 0)
                return;

            var utcNow = DateTime.UtcNow;

            await _userInterestScoreRepository.UpsertAsync(
                userId,
                "TeamId",
                teamId.ToString(),
                weight,
                utcNow);
        }

        public async Task AggregateLeagueFollowAsync(
            int userId,
            string leagueName,
            int weight)
        {
            if (userId <= 0)
                throw new InvalidOperationException("Invalid user id.");

            if (string.IsNullOrWhiteSpace(leagueName))
                throw new InvalidOperationException("League name is required.");

            if (weight <= 0)
                return;

            var utcNow = DateTime.UtcNow;

            await _userInterestScoreRepository.UpsertAsync(
                userId,
                "League",
                leagueName.Trim(),
                weight,
                utcNow);
        }
    }
}