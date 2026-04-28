using Formax.Application.Interfaces;
using System;
using System.Threading.Tasks;

namespace Formax.Application.Services
{
    public sealed class InterestEngine : IInterestEngine
    {
        private readonly IUserInterestScoreRepository _repo;
        private readonly IMatchReadRepository _matchReadRepository;

        public InterestEngine(
            IUserInterestScoreRepository repo,
            IMatchReadRepository matchReadRepository)
        {
            _repo = repo;
            _matchReadRepository = matchReadRepository;
        }

        public async Task ProcessMatchEventAsync(
            int userId,
            int matchId,
            string eventType,
            string teamName,
            DateTime occurredAtUtc)
        {
            var teamDelta = InterestWeights.TeamDelta(eventType);
            var contentDelta = InterestWeights.ContentTypeDelta(eventType);
            var leagueDelta = InterestWeights.LeagueDelta(eventType);

            await _repo.UpsertDeltaAsync(userId, "Team", teamName, teamDelta, occurredAtUtc);
            await _repo.UpsertDeltaAsync(userId, "ContentType", eventType, contentDelta, occurredAtUtc);

            var match = _matchReadRepository.GetById(matchId);
            if (match is not null && !string.IsNullOrWhiteSpace(match.League) && leagueDelta != 0)
            {
                await _repo.UpsertDeltaAsync(userId, "League", match.League, leagueDelta, occurredAtUtc);
            }
        }
    }

    internal static class InterestWeights
    {
        public static int TeamDelta(string eventType)
        {
            return eventType?.ToUpperInvariant() switch
            {
                "GOAL" => 6,
                "RED_CARD" => 5,
                "PENALTY" => 4,
                "YELLOW_CARD" => 2,
                "SUBSTITUTION" => 1,
                _ => 1
            };
        }

        public static int LeagueDelta(string eventType)
        {
            return eventType?.ToUpperInvariant() switch
            {
                "GOAL" => 3,
                "RED_CARD" => 3,
                "PENALTY" => 2,
                _ => 1
            };
        }

        public static int ContentTypeDelta(string eventType)
        {
            return eventType?.ToUpperInvariant() switch
            {
                "GOAL" => 5,
                "RED_CARD" => 4,
                "PENALTY" => 3,
                _ => 1
            };
        }
    }
}
