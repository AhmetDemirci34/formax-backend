using Formax.API.Common;
using Formax.Application.UseCases.Follow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Formax.API.Controllers
{
    /// <summary>
    /// Takip özeti. Mevcut /api/follows (maç takip) endpoint'lerini bozmaz;
    /// bu ek endpoint yalnızca sayım özeti sağlar.
    /// </summary>
    [ApiController]
    [Route("api/follow")]
    [Authorize]
    public class FollowController : ControllerBase
    {
        [HttpGet("summary")]
        public async Task<IActionResult> Summary([FromServices] GetFollowSummaryUseCase useCase)
        {
            var userId = User.GetUserId();
            if (userId == null) return Unauthorized();
            return Ok(await useCase.ExecuteAsync(userId.Value));
        }
    }
}
