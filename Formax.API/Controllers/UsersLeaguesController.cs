using Formax.API.Common;
using Formax.Application.UseCases.Leagues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Formax.API.Controllers
{
    /// <summary>
    /// Lig takip yönetimi — UsersTeamsController mimarisiyle uyumlu.
    /// GET: takip edilen ligler · POST {leagueId}: takip et · DELETE {leagueId}: bırak.
    /// </summary>
    [ApiController]
    [Route("api/users/me/leagues")]
    [Authorize]
    public class UsersLeaguesController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetMyLeagues([FromServices] GetMyLeaguesUseCase useCase)
        {
            var userId = User.GetUserId();
            if (userId == null) return Unauthorized();
            return Ok(await useCase.ExecuteAsync(userId.Value));
        }

        [HttpPost("{leagueId:int}")]
        public async Task<IActionResult> Follow(int leagueId, [FromServices] FollowLeagueUseCase useCase)
        {
            var userId = User.GetUserId();
            if (userId == null) return Unauthorized();
            await useCase.ExecuteAsync(userId.Value, leagueId);
            return Ok();
        }

        [HttpDelete("{leagueId:int}")]
        public async Task<IActionResult> Unfollow(int leagueId, [FromServices] UnfollowLeagueUseCase useCase)
        {
            var userId = User.GetUserId();
            if (userId == null) return Unauthorized();
            await useCase.ExecuteAsync(userId.Value, leagueId);
            return Ok();
        }
    }
}
