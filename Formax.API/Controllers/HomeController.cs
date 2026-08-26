using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Formax.Application.AI.World;
using Formax.Application.UseCases;
using Formax.Application.UseCases.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Formax.Infrastructure.Services.Recommendation;
using Formax.Application.Interfaces.Discovery;

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
        private readonly IDiscoveryEngine _discoveryEngine;

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
            RecommendationStatService statService,
            IDiscoveryEngine discoveryEngine)
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
            _discoveryEngine = discoveryEngine;
        }

        [AllowAnonymous]
        [HttpGet("recommendations")]
        /// <param name="maxHorizonDays">
        /// Opsiyonel zaman ufku (bugün + N takvim günü). Discover Hero/swipe kuyruğu bu
        /// parametreyle çağırır ("şimdi/çok yakında ne izlemeye değer?"). Parametresiz
        /// çağrılar — Sana Özel, Günün AI Kombini, /tumu, Trending — geniş evreni korur.
        /// </param>
        public async Task<IActionResult> GetRecommendations(
            int page = 1, int pageSize = 10, int? maxHorizonDays = null)
        {
            var userId = ReadUserIdFromClaims();

            if (userId == null || userId == 0)
                userId = 1;

            // Discovery Engine orkestrasyonu üzerinden (Recommendation üreticisini kullanır).
            var feed = await _discoveryEngine.BuildFeedAsync(userId.Value, page, pageSize, maxHorizonDays);

            // PERF: kart-başına SELECT+SaveChanges yerine tek toplu yazım.
            await _statService.RegisterImpressions(feed.Select(x => x.MatchId));

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