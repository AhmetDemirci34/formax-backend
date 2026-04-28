using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Formax.Infrastructure.Repositories;

namespace Formax.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly StateTransitionLogReadRepository _repo;

        public AdminController(StateTransitionLogReadRepository repo)
        {
            _repo = repo;
        }

        [HttpGet("ai/state-transitions")]
        public async Task<IActionResult> GetLastStateTransitions()
        {
            var result = await _repo.GetLastAsync(100);
            return Ok(result);
        }
    }
}
