using Formax.Application.DTOs.Home;
using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.Interfaces;

public interface IRecommendationEngine
{
    Task<List<RecommendationCardDto>> BuildRecommendationFeed(
        int userId,
        List<HomeRadarMatchDto> matches);
}
