using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Radar.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    /// <summary>
    /// Radar Learning (R.14.2) — exposes the user interest profile computed from
    /// LearningEvents. Read-only; not wired into ranking/recommendation.
    /// </summary>
    [ApiController]
    [Route("api/radar/interest")]
    public class RadarInterestController : ControllerBase
    {
        private readonly IUserInterestProfileService _service;

        public RadarInterestController(IUserInterestProfileService service)
        {
            _service = service;
        }

        /// <summary>GET /api/radar/interest/me — current user's interest profile.</summary>
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var userId = ResolveUserId();
            if (userId is null) return Unauthorized();
            return Ok(await _service.GetProfileAsync(userId.Value, ct));
        }

        /// <summary>GET /api/radar/interest/{userId} — explicit user (debug/admin).</summary>
        [HttpGet("{userId:int}")]
        public async Task<IActionResult> GetByUser(int userId, CancellationToken ct)
            => Ok(await _service.GetProfileAsync(userId, ct));

        private int? ResolveUserId()
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User?.FindFirstValue("sub")
                   ?? User?.FindFirstValue("userId");
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}
