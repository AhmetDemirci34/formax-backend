using System.Collections.Generic;

namespace Formax.Application.AI.UX
{
    public static class SilenceReasonUxMap
    {
        public static readonly IReadOnlyDictionary<string, string> Messages =
            new Dictionary<string, string>
            {
                ["ContextInsufficient"] =
                    "Şu an bu maç için yeterli veri yok.",

                ["StateNotAllowed"] =
                    "Bu aşamada analiz sunmak için uygun bir durum yok.",

                ["FatigueLimitReached"] =
                    "Kısa sürede çok fazla analiz yapıldı. Birazdan tekrar bakacağım.",

                ["RepetitionBlocked"] =
                    "Aynı bağlamı tekrar etmemek için sessiz kalıyorum.",

                ["RateLimited"] =
                    "AI şu anda bu maça tekrar yorum yapamıyor.",

                ["PremiumRequired"] =
                    "Bu AI yorumu sadece premium kullanıcılara açıktır."
            };

        public static string GetMessage(string? reason)
        {
            if (reason == null)
                return string.Empty;

            return Messages.TryGetValue(reason, out var message)
                ? message
                : string.Empty;
        }
    }
}
