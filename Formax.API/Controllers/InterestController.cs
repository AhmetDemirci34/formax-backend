using System;
using System.Threading.Tasks;
using Formax.API.Common;
using Formax.Application.DTOs.Interest;
using Formax.Application.UseCases.Interest;
using Formax.Infrastructure.Services.Recommendation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/interests")]

    [AllowAnonymous]
    public sealed class InterestController : ControllerBase
    {
        [HttpPost("track")]
        public async Task<IActionResult> Track(
            [FromBody] TrackInterestEventRequestDto request,
            [FromServices] TrackInterestEventUseCase useCase,
            [FromServices] RewardEventProcessor rewardProcessor)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue)
                return Unauthorized();

            // 🔹 mevcut sistem (koru)
            await useCase.ExecuteAsync(userId.Value, request, DateTime.UtcNow);

            // 🔥 YENİ: reward pipeline (FIXED)
            if (request.MatchId.HasValue && request.MatchId.Value > 0)
            {
                await rewardProcessor.ProcessEvent(
                    request.MatchId.Value,
                    request.EventType,
                    0 // dwell yok burada
                );
            }

            return Ok(new { success = true });
        }
    }
}