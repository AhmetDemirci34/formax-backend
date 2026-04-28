using Formax.Domain.States;

namespace Formax.Application.UX
{
    public static class AIUxSafetyGuard
    {
        public static string EnsureSafeMessage(
            AIUxState state,
            string? message)
        {
            // 🔕 Sessizlikte narrative tamamen kapatılır
            if (state == AIUxState.Silent || state == AIUxState.SelfRetracted)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return "Bu maç için şu an paylaşılabilecek güvenli bir değerlendirme bulunmuyor.";
            }

            return message;
        }
    }
}
