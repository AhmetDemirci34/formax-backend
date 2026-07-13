using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Radar.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    /// <summary>
    /// Radar Learning (R.14.3) — exposes user↔match affinity computed from the interest
    /// profile + match intelligence. Read-only; not wired into ranking/recommendation.
    /// </summary>
    [ApiController]
    [Route("api/radar/affinity")]
    public class RadarAffinityController : ControllerBase
    {
        private readonly IMatchAffinityService _service;

        public RadarAffinityController(IMatchAffinityService service)
        {
            _service = service;
        }

        /// <summary>GET /api/radar/affinity/match/{matchId} — current user's affinity.</summary>
        [Authorize]
        [HttpGet("match/{matchId:int}")]
        public async Task<IActionResult> GetMine(int matchId, CancellationToken ct)
        {
            var userId = ResolveUserId();
            if (userId is null) return Unauthorized();
            return Ok(await _service.GetAffinityAsync(userId.Value, matchId, ct));
        }

        /// <summary>GET /api/radar/affinity/{userId}/match/{matchId} — explicit user (debug).</summary>
        [HttpGet("{userId:int}/match/{matchId:int}")]
        public async Task<IActionResult> GetByUser(int userId, int matchId, CancellationToken ct)
            => Ok(await _service.GetAffinityAsync(userId, matchId, ct));

        private int? ResolveUserId()
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User?.FindFirstValue("sub")
                   ?? User?.FindFirstValue("userId");
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}
