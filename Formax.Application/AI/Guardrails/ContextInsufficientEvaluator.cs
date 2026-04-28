using Formax.Application.AI.Contexts;

namespace Formax.Application.AI.Guardrails
{
    public sealed class ContextInsufficientEvaluator
    {
        public bool IsInsufficient(UnifiedAiReadContext context)
        {
            if (context == null)
                return true;

            // 📌 MatchRead zorunlu
            if (context.MatchRead == null)
                return true;

            // 📌 Dakika bilgisi yoksa
            if (context.MatchRead.CurrentMinute <= 0)
                return true;

            // 📌 Olay akışı tamamen boşsa
            if (context.RecentEvents == null || context.RecentEvents.Count == 0)
                return true;

            // 📌 Kullanıcı bağlamı yoksa
            if (context.User == null)
                return true;

            return false;
        }
    }
}
