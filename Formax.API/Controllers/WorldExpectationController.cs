using Formax.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/world-expectation")]
    public sealed class WorldExpectationController : ControllerBase
    {
        private readonly WorldExpectationService _service;

        public WorldExpectationController(WorldExpectationService service)
        {
            _service = service;
        }

        [HttpGet("{matchId:int}")]
        public IActionResult Get(int matchId)
        {
            var result = _service.GetForMatch(matchId);
            return Ok(result);
        }
    }
}
