using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.Services.Feed
{
    public class UserToneEngine
    {
        public string ApplyTone(RecommendationCardDto x, string baseText, string userType)
        {
            return userType switch
            {
                "RISK" => BuildRiskTone(x),
                "SAFE" => BuildSafeTone(x),
                _ => baseText
            };
        }

        // 🔥 RISK USER → agresif, fırsat odaklı
        private string BuildRiskTone(RecommendationCardDto x)
        {
            if (x.ConfidenceScore < 0.4)
            {
                return "Bu maç yüksek risk barındırıyor ama doğru okunursa değer çıkabilir.";
            }

            if (x.ConfidenceScore < 0.6)
            {
                return $"{x.TeamA} tarafı net değil ama fırsat barındırıyor. Bu tip maçlar doğru tercih edilirse kazandırır.";
            }

            return $"{x.TeamA} önde görünüyor ve bu maç oynanabilir bir fırsat sunuyor.";
        }

        // 🔥 SAFE USER → temkinli, korumacı
        private string BuildSafeTone(RecommendationCardDto x)
        {
            if (x.ConfidenceScore < 0.4)
            {
                return "Bu maçta net bir güven yok. Temkinli yaklaşılması gerekir.";
            }

            if (x.ConfidenceScore < 0.6)
            {
                return "Maç dengede. Bu yüzden kontrollü hareket etmek daha doğru olur.";
            }

            return $"{x.TeamA} daha güven veriyor. Ancak yine de kontrollü ilerlemek önemli.";
        }
    }
}