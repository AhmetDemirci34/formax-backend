using Formax.Application.UseCases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Formax.API.Controllers
{
    /// <summary>
    /// Match Intelligence v2 — YENİ PARALEL endpoint. Mevcut MatchController'a DOKUNMAZ.
    /// Aynı "api/matches" ön ekini kullanır ama farklı action route'u ({matchId}/intelligence),
    /// bu yüzden eski {matchId}/detail ile çakışmaz. Backward compatible.
    /// </summary>
    [ApiController]
    [Route("api/matches")]
    public class MatchIntelligenceController : ControllerBase
    {
        private readonly GetMatchIntelligenceUseCase _useCase;
        private readonly ILogger<MatchIntelligenceController> _logger;

        public MatchIntelligenceController(
            GetMatchIntelligenceUseCase useCase,
            ILogger<MatchIntelligenceController> logger)
        {
            _useCase = useCase;
            _logger  = logger;
        }

        // =========================
        // ✅ MATCH INTELLIGENCE (TEK VERİ KAYNAĞI)
        // =========================
        [HttpGet("{matchId}/intelligence")]
        public async Task<IActionResult> GetMatchIntelligence(int matchId, CancellationToken cancellationToken)
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
                _logger.LogError(ex, "GetMatchIntelligence failed for matchId={MatchId}", matchId);

                return StatusCode(500, new
                {
                    error   = "Match intelligence could not be loaded",
                    message = ex.Message,
                    inner   = ex.InnerException?.Message,
                    type    = ex.GetType().Name,
                    matchId
                });
            }
        }
    }
}
