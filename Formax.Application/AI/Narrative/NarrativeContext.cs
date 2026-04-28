using System;

namespace Formax.Application.AI.Narrative
{
    public class NarrativeContext
    {
        // Kimlik
        public Guid UserId { get; init; }
        public int MatchId { get; init; }

        // Kullanıcı
        public bool IsPremium { get; init; }

        // Zaman / dil
        public DateTime UtcNow { get; init; }
        public string? Language { get; init; }

        // 🔒 FAZ-11 — OKUNAN BAĞLAM ÖZETLERİ (YORUM YOK)

        // Live
        public bool HasLiveContext { get; init; }
        public bool HasRecentEvent { get; init; }

        // PreMatch
        public bool HasPreMatchContext { get; init; }

        // Squad
        public bool HasSquadContext { get; init; }

        // World perception
        public bool HasWorldPerceptionContext { get; init; }

        // Sessizlik kararı
        public bool ShouldRemainSilent { get; init; }
    }
}
