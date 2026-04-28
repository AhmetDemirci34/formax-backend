using Formax.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/matches")]
    public class MatchController : ControllerBase
    {
        private readonly GetMatchDetailAIContextUseCase _useCase;

        public MatchController(GetMatchDetailAIContextUseCase useCase)
        {
            _useCase = useCase;
        }

        // =========================
        // ✅ MATCH DETAIL (TEK ENDPOINT)
        // =========================
        [HttpGet("{matchId}/detail")]
        public IActionResult GetMatchDetail(int matchId)
        {
            var result = _useCase.Execute(matchId);

            if (result == null)
                return NotFound();

            return Ok(result);
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