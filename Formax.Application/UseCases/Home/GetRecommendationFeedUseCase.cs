using Formax.Application.DTOs.Recommendations;
using Formax.Application.Interfaces;
using Formax.Application.Interfaces.Repositories;
using Formax.Application.Services.Radar;

namespace Formax.Application.UseCases.Home;

public sealed class GetRecommendationFeedUseCase
{
    private readonly GetHomeRadarUseCase _radarUseCase;
    private readonly IRecommendationEngine _engine;
    private readonly IUserRepository _userRepository;

    public GetRecommendationFeedUseCase(
        GetHomeRadarUseCase radarUseCase,
        IRecommendationEngine engine,
        IUserRepository userRepository)
    {
        _radarUseCase = radarUseCase;
        _engine = engine;
        _userRepository = userRepository;
    }

    public async Task<List<RecommendationCardDto>> Execute(int userId)
    {
        var user = _userRepository.GetById(userId);

        if (user == null)
            return new List<RecommendationCardDto>();

        var radar = await _radarUseCase.ExecuteAsync(
            userId,
            false,
            DateTime.UtcNow);

        if (radar?.Matches == null || radar.Matches.Count == 0)
            return new List<RecommendationCardDto>();

        var result = await _engine.BuildRecommendationFeed(
            userId,
            radar.Matches.ToList()
        );

        return result
            .OrderByDescending(x => x.Score)
            .Take(20)
            .ToList();
    }
}