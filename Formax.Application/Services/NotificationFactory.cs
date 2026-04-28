using Formax.Application.Live;
using Formax.Domain.Entities;
using System;

namespace Formax.Application.Services
{
    public class NotificationFactory
    {
        public UserNotification Create(int userId, MatchEvent matchEvent)
        {
            if (matchEvent == null)
                throw new ArgumentNullException(nameof(matchEvent));

            var (title, message) =
                NotificationTemplateProvider.Get(matchEvent);

            return new UserNotification
            {
                UserId = userId,
                MatchId = matchEvent.MatchId,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
