using Formax.Application.Interfaces;
using Formax.Application.Live;
using Formax.Application.UseCases.Follow;
using System.Threading.Tasks;

namespace Formax.Application.Services
{
    public class MatchEventNotificationService
    {
        private readonly GetUsersFollowingMatchUseCase _getUsersFollowingMatchUseCase;
        private readonly IUserNotificationRepository _notificationRepository;
        private readonly NotificationFactory _notificationFactory;

        public MatchEventNotificationService(
            GetUsersFollowingMatchUseCase getUsersFollowingMatchUseCase,
            IUserNotificationRepository notificationRepository,
            NotificationFactory notificationFactory)
        {
            _getUsersFollowingMatchUseCase = getUsersFollowingMatchUseCase;
            _notificationRepository = notificationRepository;
            _notificationFactory = notificationFactory;
        }

        public async Task HandleAsync(MatchEvent matchEvent)
        {
            var userIds =
                await _getUsersFollowingMatchUseCase.ExecuteAsync(matchEvent.MatchId);

            foreach (var userId in userIds)
            {
                var notification =
                    _notificationFactory.Create(userId, matchEvent);

                await _notificationRepository.AddAsync(notification);
            }
        }
    }
}
