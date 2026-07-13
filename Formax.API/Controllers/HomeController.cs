using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Formax.Application.AI.World;
using Formax.Application.UseCases;
using Formax.Application.UseCases.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Formax.Infrastructure.Services.Recommendation;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/home")]
    public class HomeController : ControllerBase
    {
        private readonly GetDailyAIFavoriteMatchesUseCase _getDailyAIFavoriteMatchesUseCase;
        private readonly WorldPerceptionProvider _worldPerceptionProvider;
        private readonly ResolveHomeAIStateUseCase _resolveHomeAIStateUseCase;
        private readonly WorldPerceptionCache _worldPerceptionCache;
        private readonly GetHomeAIVitrineUseCase _getHomeAIVitrineUseCase;
        private readonly GetHomeTopSapmaUseCase _getHomeTopSapmaUseCase;
        private readonly GetHomeRadarUseCase _getHomeRadarUseCase;
        private readonly GetHomeLiveSignalsUseCase _getHomeLiveSignalsUseCase;
        private readonly GetHomeNarrativeUseCase _getHomeNarrativeUseCase;
        private readonly GetRecommendationFeedUseCase _getRecommendationFeedUseCase;
        private readonly RecommendationStatService _statService;

        public HomeController(
            GetDailyAIFavoriteMatchesUseCase getDailyAIFavoriteMatchesUseCase,
            WorldPerceptionProvider worldPerceptionProvider,
            ResolveHomeAIStateUseCase resolveHomeAIStateUseCase,
            WorldPerceptionCache worldPerceptionCache,
            GetHomeAIVitrineUseCase getHomeAIVitrineUseCase,
            GetHomeTopSapmaUseCase getHomeTopSapmaUseCase,
            GetHomeRadarUseCase getHomeRadarUseCase,
            GetHomeLiveSignalsUseCase getHomeLiveSignalsUseCase,
            GetHomeNarrativeUseCase getHomeNarrativeUseCase,
            GetRecommendationFeedUseCase getRecommendationFeedUseCase,
            RecommendationStatService statService)
        {
            _getDailyAIFavoriteMatchesUseCase = getDailyAIFavoriteMatchesUseCase;
            _worldPerceptionProvider = worldPerceptionProvider;
            _resolveHomeAIStateUseCase = resolveHomeAIStateUseCase;
            _worldPerceptionCache = worldPerceptionCache;
            _getHomeAIVitrineUseCase = getHomeAIVitrineUseCase;
            _getHomeTopSapmaUseCase = getHomeTopSapmaUseCase;
            _getHomeRadarUseCase = getHomeRadarUseCase;
            _getHomeLiveSignalsUseCase = getHomeLiveSignalsUseCase;
            _getHomeNarrativeUseCase = getHomeNarrativeUseCase;
            _getRecommendationFeedUseCase = getRecommendationFeedUseCase;
            _statService = statService;
        }

        [AllowAnonymous]
        [HttpGet("recommendations")]
        public async Task<IActionResult> GetRecommendations(int page = 1, int pageSize = 10)
        {
            var userId = ReadUserIdFromClaims();

            if (userId == null || userId == 0)
                userId = 1;

            var feed = await _getRecommendationFeedUseCase.Execute(userId.Value, page, pageSize);

            foreach (var item in feed)
            {
                await _statService.RegisterImpression(item.MatchId);
            }

            return Ok(feed);
        }

        private int? ReadUserIdFromClaims()
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub")
                ?? User?.FindFirstValue("userId");

            return int.TryParse(raw, out var userId) ? userId : null;
        }
    }
}