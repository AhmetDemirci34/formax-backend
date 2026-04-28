using System.Collections.Generic;
using Formax.Application.Live;
using Formax.Application.AI.Guardrails;

namespace Formax.Application.AI.Narrative
{
    public static class AiSpeakGate
    {
        public static bool CanSpeak(
            IReadOnlyList<MatchEvent> events,
            bool isPremiumUser,
            int aiUsageCount)
        {
            // 1️⃣ Anlamsal kırılma var mı?
            var hasSemanticBreak =
                SemanticBreakDetector.HasSemanticBreak(events);

            // 2️⃣ Guardrail politikası son kararı verir
            return AiSpeakPermission.CanSpeak(
                isPremiumUser,
                hasSemanticBreak,
                aiUsageCount
            );
        }
    }
}
