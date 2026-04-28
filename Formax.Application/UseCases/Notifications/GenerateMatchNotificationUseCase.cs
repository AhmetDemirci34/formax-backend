using Formax.Application.DTOs.Notifications;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Notifications
{
    public class GenerateMatchNotificationUseCase
    {
        private readonly IMatchReadRepository _matchReadRepository;

        public GenerateMatchNotificationUseCase(IMatchReadRepository matchReadRepository)
        {
            _matchReadRepository = matchReadRepository;
        }

        public AINotificationDto? Execute(int matchId)
        {
            var match = _matchReadRepository
                .Query()
                .FirstOrDefault(m => m.Id == matchId);

            if (match == null)
                return null;

            var minute = 0;
            if (!string.IsNullOrWhiteSpace(match.MatchMinute))
                int.TryParse(match.MatchMinute, out minute);

            var notification = new AINotificationDto
            {
                MatchId = match.Id,
                IsPremiumContent = true
            };

            if (minute >= 65 && minute <= 75)
            {
                notification.Title = "Maç kritik eşikte";
                notification.Message =
                    "AI, maçın kaderini değiştirebilecek bir evreye girildiğini tespit etti.";
                notification.IsCritical = true;
            }
            else if (minute > 75)
            {
                notification.Title = "Son dakikalara girildi";
                notification.Message =
                    "Maçta risk seviyesi arttı. Ani gelişmeler yaşanabilir.";
                notification.IsCritical = false;
            }
            else
            {
                return null; // Bildirim gerekmez
            }

            return notification;
        }
    }
}
