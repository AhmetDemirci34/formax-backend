using Formax.Application.UseCases.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/ai-metrics")]
    public class AiSpeakMetricsController : ControllerBase
    {
        private readonly GetAiSpeakMetricsUseCase _useCase;

        public AiSpeakMetricsController(
            GetAiSpeakMetricsUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var result = _useCase.Execute();
            return Ok(result);
        }
    }
}
