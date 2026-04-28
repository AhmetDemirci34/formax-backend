using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.Services.Feed
{
    public class LabelEngine
    {
        public void Apply(List<RecommendationCardDto> list)
        {
            foreach (var item in list)
            {
                // 🔒 LOCK VARSA DOKUNMA
                if (item.Highlight != null)
                    continue;

                // fallback sadece boşsa çalışır
                if (string.IsNullOrEmpty(item.InsightLabel))
                {
                    item.InsightLabel = "GOOD";
                    item.InsightReason = "Fallback";
                }
            }
        }
    }
}