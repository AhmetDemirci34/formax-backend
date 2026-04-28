using Formax.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/formax/analysis")]
    public class AIAnalysisController : ControllerBase
    {
        private readonly IAIAnalysisService _service;

        public AIAnalysisController(IAIAnalysisService service)
        {
            _service = service;
        }

        [HttpGet("match/{matchId}")]
        public IActionResult AnalyzeMatch(int matchId)
        {
            var result = _service.AnalyzeMatch(matchId);
            return Ok(result);
        }

        [HttpGet("coupon/{couponId}")]
        public IActionResult AnalyzeCoupon(int couponId)
        {
            var result = _service.AnalyzeCoupon(couponId);
            return Ok(result);
        }
    }
}
