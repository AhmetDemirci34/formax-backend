using Microsoft.AspNetCore.Mvc;
using Formax.Application.UseCases.Teams;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/teams")]
    public class TeamsController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAll([FromServices] GetTeamsUseCase useCase)
        {
            return Ok(await useCase.ExecuteAsync());
        }
    }
}
