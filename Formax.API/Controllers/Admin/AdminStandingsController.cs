using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// TEST-ONLY manual trigger for the standings refresh + Team.LeagueRank bridge.
    /// The production schedule (04:00 UTC daily batch in WorldPerceptionDailyJob)
    /// is unchanged and remains the only automatic trigger. This endpoint exists
    /// solely to validate Sprint 15C without waiting for the batch window.
    /// </summary>
    [ApiController]
    [Route("admin/standings")]
    public class AdminStandingsController : ControllerBase
    {
        private readonly WorldPerceptionDailyJob _job;

        public AdminStandingsController(WorldPerceptionDailyJob job)
        {
            _job = job;
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken ct)
        {
            await _job.RefreshStandingsAsync(ct);
            return Ok(new { message = "standings refresh triggered" });
        }
    }
}
