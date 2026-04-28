using Formax.Application.DTOs.Home;
using Formax.Application.DTOs.Recommendations;
using Formax.Application.Interfaces;
using Formax.Application.Services.Recommendation;

namespace Formax.Application.Services;

public class RecommendationService : IRecommendationService
{
    private readonly IRecommendationEngine _engine;

    public RecommendationService(IRecommendationEngine engine)
    {
        _engine = engine;
    }

    public async Task<List<RecommendationCardDto>> GetFeedAsync(
        int userId,
        List<HomeRadarMatchDto> matches)
    {
        // 🔥 BURASI SADECE ENGINE ÇAĞIRIR
        return await _engine.BuildRecommendationFeed(userId, matches);
    }

    public Task<List<RecommendationCardDto>> GetFeedAsync(int userId)
    {
        throw new NotImplementedException();
    }
}