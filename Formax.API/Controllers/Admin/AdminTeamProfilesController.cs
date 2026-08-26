using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// TEST-ONLY manual trigger for the Phase 6 Final team profile refresh
    /// (api-football /coachs + /teams + /players/squads + /transfers → TeamProfileSignals).
    /// AI/GDP-only. Production schedule (04:00 UTC batch in WorldPerceptionDailyJob) unchanged.
    /// </summary>
    [ApiController]
    [Route("admin/teamprofiles")]
    public class AdminTeamProfilesController : ControllerBase
    {
        private readonly WorldPerceptionDailyJob _job;

        public AdminTeamProfilesController(WorldPerceptionDailyJob job)
        {
            _job = job;
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken ct)
        {
            await _job.RefreshTeamProfilesAsync(ct);
            return Ok(new { message = "team profiles refresh triggered" });
        }
    }
}
