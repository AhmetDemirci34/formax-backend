using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// TEST-ONLY manual trigger for the Phase 6 / Slice 2 match prediction refresh
    /// (api-football /predictions → MatchPredictionSignals). AI-ONLY signal; never surfaced
    /// to users. The production schedule (04:00 UTC daily batch in WorldPerceptionDailyJob)
    /// is unchanged. This endpoint validates the ingestion without waiting for the batch window.
    /// </summary>
    [ApiController]
    [Route("admin/predictions")]
    public class AdminPredictionsController : ControllerBase
    {
        private readonly WorldPerceptionDailyJob _job;

        public AdminPredictionsController(WorldPerceptionDailyJob job)
        {
            _job = job;
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken ct)
        {
            await _job.RefreshMatchPredictionsAsync(ct);
            return Ok(new { message = "match predictions refresh triggered" });
        }
    }
}
