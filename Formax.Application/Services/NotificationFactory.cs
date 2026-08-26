using Formax.Application.Live;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
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

            var eventType = MapEventType(matchEvent.EventType);

            return new UserNotification
            {
                UserId = userId,
                MatchId = matchEvent.MatchId,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,

                // Frontend'in ikon + filtreleme + yönlendirme için ihtiyaç duyduğu alanlar.
                EventType = eventType,
                Category = MapCategory(eventType),
                TargetType = MapTargetType(eventType),
                TargetId = matchEvent.MatchId
                // NOT: MatchEvent'te TeamId/LeagueId/LogoUrl yok → null bırakılır (uydurulmaz).
            };
        }

        /// <summary>Canlı maç event türünü bildirim türüne çevirir.</summary>
        private static NotificationEventType MapEventType(MatchEventType type) => type switch
        {
            MatchEventType.MatchStarted => NotificationEventType.MatchStarted,
            MatchEventType.MatchEnded => NotificationEventType.MatchFinished,
            MatchEventType.Goal => NotificationEventType.Goal,
            MatchEventType.RedCardAwarded => NotificationEventType.RedCard,
            MatchEventType.RedCardConfirmed => NotificationEventType.RedCard,
            _ => NotificationEventType.Unknown
        };

        private static NotificationCategory MapCategory(NotificationEventType type) => type switch
        {
            NotificationEventType.Transfer => NotificationCategory.News,
            NotificationEventType.News => NotificationCategory.News,
            _ => NotificationCategory.Match
        };

        private static NotificationTargetType MapTargetType(NotificationEventType type) => type switch
        {
            NotificationEventType.Lineup => NotificationTargetType.MatchLineup,
            NotificationEventType.AIAnalysis => NotificationTargetType.MatchAIAnalysis,
            NotificationEventType.News => NotificationTargetType.News,
            NotificationEventType.Transfer => NotificationTargetType.News,
            NotificationEventType.AICombo => NotificationTargetType.AICombo,
            _ => NotificationTargetType.Match
        };
    }
}
