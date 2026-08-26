using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// TEST-ONLY manual trigger for the per-team Timeline sync (Fixture Expansion v2).
    /// The 12 h schedule in HistoricalSyncJob remains the only automatic trigger.
    /// </summary>
    [ApiController]
    [Route("admin/historical-sync")]
    public class AdminHistoricalSyncController : ControllerBase
    {
        private readonly HistoricalSyncJob _job;

        public AdminHistoricalSyncController(HistoricalSyncJob job)
        {
            _job = job;
        }

        /// <summary>Runs one watermark-driven cycle (Cold Start first, then Incremental).</summary>
        [HttpPost("run")]
        public async Task<IActionResult> Run(CancellationToken ct)
        {
            var added = await _job.RunCycleAsync(ct);
            return Ok(new { matchesAdded = added });
        }

        /// <summary>Targeted single-team Timeline sync (last=N + next=N) for proof.</summary>
        [HttpPost("run-team")]
        public async Task<IActionResult> RunTeam([FromQuery] string externalTeamId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(externalTeamId))
                return BadRequest(new { error = "externalTeamId is required" });

            var added = await _job.RunForTeamAsync(externalTeamId, ct);
            return Ok(new { externalTeamId, matchesAdded = added });
        }
    }
}
