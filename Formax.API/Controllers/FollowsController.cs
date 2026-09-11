using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/follows")]
    [Authorize]
    public class FollowsController : ControllerBase
    {
        private int GetUserId()
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User?.FindFirstValue("sub")
                      ?? User?.FindFirstValue("userId");
            return int.TryParse(raw, out var id) ? id : 0;
        }

        [HttpPost("{matchId}")]
        public async Task<IActionResult> Follow(
            int matchId,
            [FromServices] FollowMatchUseCase useCase)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();
            await useCase.ExecuteAsync(userId, matchId);
            return Ok();
        }

        [HttpDelete("{matchId}")]
        public async Task<IActionResult> Unfollow(
            int matchId,
            [FromServices] UnfollowMatchUseCase useCase)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();
            await useCase.ExecuteAsync(userId, matchId);
            return Ok();
        }

        [HttpGet("me")]
        public async Task<IActionResult> MyFollows(
            [FromServices] GetFollowedMatchesUseCase useCase)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();
            return Ok(await useCase.ExecuteAsync(userId));
        }

        /// <summary>
        /// "TAKİP ETTİĞİM MAÇLAR" EKRANI — yalnız kullanıcının kendi maç takipleri.
        ///
        /// Mevcut <c>GET me</c> ucundan farkı: durum DEPODAN okunur (saatten "Live"
        /// türetilmez), skor yalnız bitmiş maçta döner ve liste ekranın istediği iki
        /// bölüme AYRILMIŞ hâlde gelir. Takım/lig takipleri bu yanıtta YOKTUR.
        /// Salt DB — sağlayıcıya sıfır istek.
        /// </summary>
        [HttpGet("me/matches")]
        public async Task<IActionResult> MyFollowedMatches(
            [FromServices] Formax.Application.UseCases.Follow.GetFollowedMatchesScreenUseCase useCase,
            System.Threading.CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();
            return Ok(await useCase.ExecuteAsync(userId, System.DateTime.UtcNow, ct));
        }

        /// <summary>Returns the match IDs the current user follows — lightweight endpoint for UI state.</summary>
        [HttpGet("me/ids")]
        public async Task<IActionResult> MyFollowIds(
            [FromServices] IUserMatchFollowRepository repository)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized();
            var follows = await repository.GetByUserAsync(userId);
            return Ok(follows.Select(f => f.MatchId).ToList());
        }
    }
}
