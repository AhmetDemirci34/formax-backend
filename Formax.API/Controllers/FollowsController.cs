using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/follows")]
    public class FollowsController : ControllerBase
    {
        [HttpPost("{matchId}")]
        public async Task<IActionResult> Follow(
            int matchId,
            [FromServices] FollowMatchUseCase useCase)
        {
            int userId = 1; // şimdilik sabit
            await useCase.ExecuteAsync(userId, matchId);
            return Ok();
        }

        [HttpDelete("{matchId}")]
        public async Task<IActionResult> Unfollow(
            int matchId,
            [FromServices] UnfollowMatchUseCase useCase)
        {
            int userId = 1;
            await useCase.ExecuteAsync(userId, matchId);
            return Ok();
        }

        [HttpGet("me")]
        public async Task<IActionResult> MyFollows(
            [FromServices] GetFollowedMatchesUseCase useCase)
        {
            int userId = 1;
            return Ok(await useCase.ExecuteAsync(userId));
        }
    }
}
