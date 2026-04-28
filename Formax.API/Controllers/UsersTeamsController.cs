using Formax.API.Common;
using Formax.Application.DTOs.Teams;
using Formax.Application.Interfaces;
using Formax.Application.UseCases.Teams;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/users/me/teams")]
    public class UsersTeamsController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetMyTeams([FromServices] GetMyTeamsUseCase useCase)
        {
            int userId = 1; // şimdilik sabit (senin mevcut pattern’in)
            return Ok(await useCase.ExecuteAsync(userId));
        }

        [HttpPost]
        public async Task<IActionResult> SetMyTeams(
            [FromBody] SetMyTeamsRequest request,
            [FromServices] SetMyTeamsUseCase useCase)
        {
            int userId = 1;
            await useCase.ExecuteAsync(userId, request.TeamIds);
            return Ok();
        }

        [HttpGet("~/api/users/me/subscription")]
        public IActionResult GetMySubscription(
        [FromServices] IUserRepository userRepository)
        {
            var userId = User.GetUserId();

            if (userId == null)
                return Unauthorized();

            var user = userRepository.GetById(userId.Value);

            if (user == null)
                return NotFound();

            return Ok(new
            {
                isPremium = user.IsPremium,
                accessLevel = user.IsPremium ? "Premium" : "Free"
            });
        }
    }
}