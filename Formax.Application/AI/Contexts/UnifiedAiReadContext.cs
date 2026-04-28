using System.Collections.Generic;
using Formax.Application.Live;
using Formax.Application.States;

namespace Formax.Application.AI.Contexts
{
    /// <summary>
    /// AI'nin karar anında TEK YERDEN okuduğu
    /// birleşik, read-only bağlam.
    /// </summary>
    public sealed class UnifiedAiReadContext
    {
        // 📌 MAÇ OKUMASI (MEVCUT)
        public AiReadContext MatchRead { get; init; } = null!;
        public IReadOnlyList<MatchEvent> RecentEvents { get; init; } = new List<MatchEvent>();

        // 📌 KULLANICI OKUMASI (MEVCUT)
        public UserExperienceContext User { get; init; } = null!;

        // 📌 HAZIR OKUMA FLAG'LERİ (MEVCUT)
        public bool SemanticBreakDetected { get; init; }
        public bool IsPremiumUser { get; init; }
        public int AiUsageCount { get; init; }

        // 📌 STATE (MEVCUT)
        public UserState UserState { get; init; }

        // 🔒 FAZ-9.3 — YENİ ALT CONTEXT'LER (PASİF)

        public LiveMatchContext LiveMatch { get; init; } = null!;
        public PreMatchContext PreMatch { get; init; } = null!;
        public SquadContext? Squad { get; set; }
        public WorldPerceptionContext WorldPerception { get; init; } = null!;

    }
}
