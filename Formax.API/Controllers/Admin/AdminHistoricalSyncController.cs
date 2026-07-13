using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// TEST-ONLY manual trigger for the historical backfill (Sprint 19B).
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

        [HttpPost("run")]
        public async Task<IActionResult> Run(CancellationToken ct)
        {
            var added = await _job.RunCycleAsync(ct);
            return Ok(new { matchesAdded = added });
        }
    }
}
