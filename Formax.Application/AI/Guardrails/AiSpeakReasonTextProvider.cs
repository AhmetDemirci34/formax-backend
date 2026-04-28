using System.Collections.Generic;

namespace Formax.Application.AI.Guardrails
{
    public static class AiSpeakReasonTextProvider
    {
        private static readonly IReadOnlyDictionary<AiSpeakReason, string> Texts
            = new Dictionary<AiSpeakReason, string>
        {
            {
                AiSpeakReason.Allowed,
                "Bu an için aktarılabilecek yeni bir bağlam mevcut."
            },
            {
                AiSpeakReason.RateLimited,
                "Kısa süre önce bilgi paylaşıldı. Yeni bağlam için biraz bekleniyor."
            },
            {
                AiSpeakReason.ContextInsufficient,
                "Bu an için yeterli bağlam oluşmadı."
            },
            {
                AiSpeakReason.StateBlocked,
                "Mevcut durum yeni bir yorum için uygun değil."
            }
        };

        public static string GetText(AiSpeakReason reason)
        {
            return Texts.TryGetValue(reason, out var text)
                ? text
                : Texts[AiSpeakReason.ContextInsufficient];
        }
    }
}
    