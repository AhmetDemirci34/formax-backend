using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// TEST-ONLY manual trigger for the Phase 6 team season statistics refresh
    /// (api-football /teams/statistics → TeamSeasonStatistics). The production schedule
    /// (04:00 UTC daily batch in WorldPerceptionDailyJob) is unchanged. This endpoint
    /// validates the ingestion without waiting for the batch window.
    /// </summary>
    [ApiController]
    [Route("admin/teamstats")]
    public class AdminTeamStatisticsController : ControllerBase
    {
        private readonly WorldPerceptionDailyJob _job;

        public AdminTeamStatisticsController(WorldPerceptionDailyJob job)
        {
            _job = job;
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken ct)
        {
            await _job.RefreshTeamStatisticsAsync(ct);
            return Ok(new { message = "team statistics refresh triggered" });
        }
    }
}
