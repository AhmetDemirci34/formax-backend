using Formax.Domain.States;

namespace Formax.Application.States
{
    public class StateDecisionContext
    {
        public int MatchId { get; set; }

        public double ConfidenceScore { get; set; }

        public bool GuardrailBlocked { get; set; }

        public bool HasConflict { get; set; }

        // 🔥 FAZ-8 — ŞU ANKİ BAĞLAM
        public AIContextKey CurrentContextKey { get; set; } = AIContextKey.None;

        // 🔥 FAZ-8 — SON GENİŞLETİLEN BAĞLAM
        public AIContextKey LastExtendedContextKey { get; set; } = AIContextKey.None;

        public DateTime? LastExtendedAt { get; set; }

        // 🔒 SPRINT-6 — PREMIUM GATE
        public bool IsPremiumUser { get; set; }

        // 🔥 SPRINT-9 — MEMORY EROSION (DEFAULT SAFE)
        public double ContextMemoryWeight { get; set; } = 1.0;
    }
}
