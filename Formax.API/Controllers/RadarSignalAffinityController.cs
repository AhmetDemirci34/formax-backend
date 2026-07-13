using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Radar.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    /// <summary>
    /// Radar Learning (R.14.5) — exposes the user's radar (signal) affinity, projected
    /// from the interest profile. Read-only; not wired into ranking/recommendation/feed.
    /// </summary>
    [ApiController]
    [Route("api/radar/affinity/signals")]
    public class RadarSignalAffinityController : ControllerBase
    {
        private readonly IRadarAffinityService _service;

        public RadarSignalAffinityController(IRadarAffinityService service)
        {
            _service = service;
        }

        /// <summary>GET /api/radar/affinity/signals/me — current user's radar affinity.</summary>
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var userId = ResolveUserId();
            if (userId is null) return Unauthorized();
            return Ok(await _service.GetAsync(userId.Value, ct));
        }

        /// <summary>GET /api/radar/affinity/signals/{userId} — explicit user (debug).</summary>
        [HttpGet("{userId:int}")]
        public async Task<IActionResult> GetByUser(int userId, CancellationToken ct)
            => Ok(await _service.GetAsync(userId, ct));

        private int? ResolveUserId()
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User?.FindFirstValue("sub")
                   ?? User?.FindFirstValue("userId");
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}
