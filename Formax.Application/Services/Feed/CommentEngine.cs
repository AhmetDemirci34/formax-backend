using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.Services.Feed
{
    public class CommentEngine
    {
        public string GenerateWithEdge(RecommendationCardDto x)
        {
            // 🔒 LABEL ASLA DEĞİŞMEZ
            var label = x.InsightLabel;

            // 🔥 EXTERNAL INFO (SADECE TEXT)
            if (x.ExternalTrend?.IsHot == true)
                return "Oranlar hızlı değişiyor. Piyasa hareketli.";

            switch (label)
            {
                case "RISKY":
                    return "Yüksek risk. Dikkat.";

                case "HOT":
                    return "Güçlü fırsat.";

                case "GOOD":
                    return "Dengeli tercih.";

                case "VALUE":
                case "STRONG_BUY":
                    return "Piyasa kaçırıyor olabilir.";

                case "BAD":
                    return "Uzak dur.";

                default:
                    return "Nötr.";
            }
        }
    }
}