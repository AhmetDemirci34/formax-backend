using Formax.Application.DTOs.Notifications;
using Formax.Application.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Notifications
{
    public class GetUserNotificationsUseCase
    {
        private readonly IUserNotificationRepository _repository;

        public GetUserNotificationsUseCase(IUserNotificationRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<UserNotificationDto>> ExecuteAsync(int userId)
        {
            var notifications = await _repository.GetByUserAsync(userId);

            return notifications.Select(n => new UserNotificationDto
            {
                Id = n.Id,
                MatchId = n.MatchId,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,

                EventType = n.EventType.ToString(),
                Category = n.Category.ToString(),
                LogoUrl = n.LogoUrl,
                TeamId = n.TeamId,
                LeagueId = n.LeagueId,
                TargetType = n.TargetType.ToString(),
                // Legacy satırlar için hedef id yoksa maça düş (frontend her zaman yönlenebilsin).
                TargetId = n.TargetId ?? n.MatchId,
                Type = n.NotificationType,
                Route = n.Route ?? (n.MatchId > 0 ? $"/match/{n.MatchId}" : null)
            }).ToList();
        }
    }
}

