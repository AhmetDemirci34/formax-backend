using System.Collections.Generic;
using Formax.Application.Live;

namespace Formax.Application.AI.Narrative
{
    /// <summary>
    /// Timeline olaylarına bakarak
    /// AI için anlamsal bir kırılma var mı?
    /// </summary>
    public static class SemanticBreakDetector
    {
        public static bool HasSemanticBreak(
            IReadOnlyList<MatchEvent> events)
        {
            if (events == null || events.Count == 0)
                return false;

            foreach (var e in events)
            {
                // 🟢 Gol = anlamsal kırılma
                if (e.EventType == MatchEventType.Goal)
                    return true;

                // 🔴 Kırmızı kart süreci = anlamsal kırılma
                if (e.EventType == MatchEventType.RedCardAwarded)
                    return true;

                if (e.EventType == MatchEventType.RedCardConfirmed)
                    return true;

                // ⚠️ VAR inceleme ve iptal aşamaları şimdilik kırılma sayılmaz
            }

            return false;
        }
    }
}
