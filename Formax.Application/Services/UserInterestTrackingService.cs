using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using System.Globalization;
using System.Text;

namespace Formax.Application.Services
{
    public sealed class UserInterestTrackingService : IUserInterestTrackingService
    {
        private readonly IUserInterestEventRepository _eventRepository;
        private readonly IUserInterestScoreRepository _scoreRepository;
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly ITeamReadRepository _teamReadRepository;
        private readonly IInterestAggregationService _interestAggregationService;

        public UserInterestTrackingService(
            IUserInterestEventRepository eventRepository,
            IUserInterestScoreRepository scoreRepository,
            IMatchReadRepository matchReadRepository,
            ITeamReadRepository teamReadRepository,
            IInterestAggregationService interestAggregationService)
        {
            _eventRepository = eventRepository;
            _scoreRepository = scoreRepository;
            _matchReadRepository = matchReadRepository;
            _teamReadRepository = teamReadRepository;
            _interestAggregationService = interestAggregationService;
        }

        public async Task TrackAsync(
            int userId,
            string eventType,
            int? matchId,
            int? teamId,
            string? leagueName,
            DateTime utcNow)
        {
            var normalizedType = NormalizeEventType(eventType);
            var weight = ResolveWeight(normalizedType);

            var trimmedLeague = Normalize(leagueName ?? "");

            await _eventRepository.AddAsync(new UserInterestEvent
            {
                UserId = userId,
                EventType = normalizedType,
                MatchId = matchId,
                TeamId = teamId,
                LeagueName = trimmedLeague,
                Weight = weight,
                CreatedAtUtc = utcNow
            });

            var touchedTeamNames = new HashSet<string>();

            if (matchId.HasValue)
            {
                var match = _matchReadRepository.GetById(matchId.Value);
                if (match != null)
                {
                    trimmedLeague = string.IsNullOrWhiteSpace(trimmedLeague)
                        ? Normalize(match.League)
                        : trimmedLeague;

                    var home = _teamReadRepository.GetById(match.HomeTeamId);
                    var away = _teamReadRepository.GetById(match.AwayTeamId);

                    if (home != null)
                        touchedTeamNames.Add(Normalize(home.Name));

                    if (away != null)
                        touchedTeamNames.Add(Normalize(away.Name));
                }
            }

            foreach (var team in touchedTeamNames)
            {
                await _scoreRepository.UpsertDeltaAsync(userId, "team", team, weight, utcNow);
            }

            if (!string.IsNullOrWhiteSpace(trimmedLeague))
            {
                await _scoreRepository.UpsertDeltaAsync(userId, "league", trimmedLeague, weight, utcNow);
            }

            await _scoreRepository.UpsertDeltaAsync(
                userId,
                "contenttype",
                Normalize(normalizedType.ToString()),
                weight,
                utcNow);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                var category = Char.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static InterestEventType NormalizeEventType(string value)
        {
            var key = (value ?? "").Trim().ToLowerInvariant();

            return key switch
            {
                "click" => InterestEventType.MatchClick,
                "view" => InterestEventType.MatchView,
                "open" => InterestEventType.DepthOpen,
                _ => InterestEventType.MatchView
            };
        }

        private static int ResolveWeight(InterestEventType type)
        {
            return type switch
            {
                InterestEventType.MatchClick => 3,
                InterestEventType.MatchView => 1,
                InterestEventType.DepthOpen => 6,
                _ => 1
            };
        }
    }
}