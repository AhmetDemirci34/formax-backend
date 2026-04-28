using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.Services.Feed
{
    public class AdaptiveRankingService
    {
        public List<RecommendationCardDto> Rank(List<RecommendationCardDto> list, string userType)
        {
            // 🔥 SCORE'A DOKUNMA
            // sadece sıralama yap

            return list
                .OrderByDescending(x => x.Score)
                .ToList();
        }
    }
}