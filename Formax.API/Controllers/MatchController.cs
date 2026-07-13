using Formax.Application.UseCases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/matches")]
    public class MatchController : ControllerBase
    {
        private readonly GetMatchDetailAIContextUseCase _useCase;
        private readonly ILogger<MatchController> _logger;

        public MatchController(GetMatchDetailAIContextUseCase useCase, ILogger<MatchController> logger)
        {
            _useCase = useCase;
            _logger  = logger;
        }

        // =========================
        // ✅ MATCH DETAIL (TEK ENDPOINT)
        // =========================
        [HttpGet("{matchId}/detail")]
        public async Task<IActionResult> GetMatchDetail(int matchId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _useCase.ExecuteAsync(matchId, cancellationToken);

                if (result == null)
                    return NotFound(new { error = "Match not found", matchId });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMatchDetail failed for matchId={MatchId}", matchId);

                return StatusCode(500, new
                {
                    error   = "Match detail could not be loaded",
                    message = ex.Message,
                    inner   = ex.InnerException?.Message,
                    type    = ex.GetType().Name,
                    matchId
                });
            }
        }

        // =========================
        // ✅ FEED
        // =========================
        [HttpGet]
        public IActionResult GetMatches()
        {
            var list = new[]
            {
                new {
                    matchId = 1,
                    radarScore = 52,
                    playRate = 60,
                    trendDelta = 3,
                    teams = new {
                        home = "Galatasaray",
                        away = "Fenerbahçe"
                    }
                },
                new {
                    matchId = 2,
                    radarScore = 48,
                    playRate = 55,
                    trendDelta = -2,
                    teams = new {
                        home = "Barcelona",
                        away = "Atletico"
                    }
                }
            };

            return Ok(list);
        }
    }
}