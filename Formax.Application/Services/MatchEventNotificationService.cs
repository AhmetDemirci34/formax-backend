using Formax.Application.Interfaces;
using Formax.Application.Live;
using Formax.Application.UseCases.Follow;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Formax.Application.Services
{
    /// <summary>
    /// Maç event'i (gol/kart/...) → bildirim fan-out. MVP FIX: yalnız MAÇ-takipçileri değil,
    /// maçtaki iki takımı TAKİP EDEN ve maçın ligini takip eden kullanıcılara da bildirim üretir
    /// (birleşim + dedupe). Böylece "takım takip et → maçında bildirim al" çalışır.
    /// </summary>
    public class MatchEventNotificationService
    {
        private readonly GetUsersFollowingMatchUseCase _getUsersFollowingMatchUseCase;
        private readonly IUserTeamFollowRepository _teamFollowRepository;
        private readonly IUserLeagueFollowRepository _leagueFollowRepository;
        private readonly IMatchReadRepository _matchRepository;
        private readonly IUserNotificationRepository _notificationRepository;
        private readonly NotificationFactory _notificationFactory;

        public MatchEventNotificationService(
            GetUsersFollowingMatchUseCase getUsersFollowingMatchUseCase,
            IUserTeamFollowRepository teamFollowRepository,
            IUserLeagueFollowRepository leagueFollowRepository,
            IMatchReadRepository matchRepository,
            IUserNotificationRepository notificationRepository,
            NotificationFactory notificationFactory)
        {
            _getUsersFollowingMatchUseCase = getUsersFollowingMatchUseCase;
            _teamFollowRepository = teamFollowRepository;
            _leagueFollowRepository = leagueFollowRepository;
            _matchRepository = matchRepository;
            _notificationRepository = notificationRepository;
            _notificationFactory = notificationFactory;
        }

        public async Task HandleAsync(MatchEvent matchEvent)
        {
            // 1) Doğrudan MAÇ takipçileri.
            var userIds = new HashSet<int>(
                await _getUsersFollowingMatchUseCase.ExecuteAsync(matchEvent.MatchId));

            // 2) Maçtaki iki takımı + ligini takip edenler (ters lookup). Maç yüklenemezse yalnız (1).
            var match = _matchRepository.GetById(matchEvent.MatchId);
            if (match != null)
            {
                foreach (var uid in await _teamFollowRepository.GetUserIdsByTeamAsync(match.HomeTeamId))
                    userIds.Add(uid);
                foreach (var uid in await _teamFollowRepository.GetUserIdsByTeamAsync(match.AwayTeamId))
                    userIds.Add(uid);
                foreach (var uid in await _leagueFollowRepository.GetUserIdsByLeagueAsync(match.LeagueId))
                    userIds.Add(uid);
            }

            // 3) Fan-out (dedupe edilmiş kullanıcı kümesi → maç başına kullanıcıya tek bildirim).
            foreach (var userId in userIds)
            {
                var notification = _notificationFactory.Create(userId, matchEvent);
                await _notificationRepository.AddAsync(notification);
            }
        }
    }
}
