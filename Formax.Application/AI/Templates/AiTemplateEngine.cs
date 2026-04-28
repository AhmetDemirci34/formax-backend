using Formax.Application.AI.Contexts;

namespace Formax.Application.AI.Templates
{
    public static class AiTemplateEngine
    {
        public static string SelectTemplate(UnifiedAiReadContext ctx)
        {
            // 🔒 Anlamsal kırılma yoksa → sessizlik
            if (!ctx.SemanticBreakDetected)
                return "T1";

            // 🔒 Free kullanıcı → kısa metin
            if (!ctx.IsPremiumUser)
                return "T2";

            // 🔒 Premium kullanıcı → orta metin
            return "T3";
        }
    }
}
