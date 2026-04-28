using Formax.Application.Live;

namespace Formax.Application.Services
{
    public static class NotificationSoundProvider
    {
        public static string GetSound(MatchEvent matchEvent)
        {
            return matchEvent.EventType switch
            {
                MatchEventType.Goal =>
                    NotificationSoundType.Goal,

                MatchEventType.PenaltyAwarded or
                MatchEventType.PenaltyConfirmed =>
                    NotificationSoundType.Penalty,

                MatchEventType.RedCardAwarded or
                MatchEventType.RedCardConfirmed =>
                    NotificationSoundType.RedCard,

                MatchEventType.PenaltyVarReviewStarted or
                MatchEventType.RedCardVarReviewStarted =>
                    NotificationSoundType.Var,

                MatchEventType.MatchStarted =>
                    NotificationSoundType.MatchStart,

                MatchEventType.MatchEnded =>
                    NotificationSoundType.MatchEnd,

                _ =>
                    NotificationSoundType.Generic
            };
        }
    }
}
