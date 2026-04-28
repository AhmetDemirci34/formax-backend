using System;
using Formax.Application.AI.Contexts;

namespace Formax.Application.AI.Narrative
{
    public static class NarrativeContextMapper
    {
        public static NarrativeContext Map(
            UnifiedAiReadContext unified,
            Guid userId,
            int matchId,
            bool shouldRemainSilent,
            string? language)
        {
            return new NarrativeContext
            {
                // Kimlik
                UserId = userId,
                MatchId = matchId,

                // Kullanıcı
                IsPremium = unified.IsPremiumUser,

                // Zaman / dil
                UtcNow = DateTime.UtcNow,
                Language = language,

                // 🔒 OKUNAN BAĞLAM BAYRAKLARI

                // Live
                HasLiveContext = unified.LiveMatch != null,
                HasRecentEvent =
                    unified.RecentEvents != null &&
                    unified.RecentEvents.Count > 0,

                // PreMatch
                HasPreMatchContext = unified.PreMatch != null,

                // Squad
                HasSquadContext = unified.Squad != null,

                // World perception
                HasWorldPerceptionContext = unified.WorldPerception != null,

                // Sessizlik (state/guard sonucu dışarıdan gelir)
                ShouldRemainSilent = shouldRemainSilent
            };
        }
    }
}
