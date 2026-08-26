using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// MANUEL tetik — canonical CompetitionContext coverage doldurma (GDP Final Coverage sprint).
    /// Üretim programı (04:00 UTC batch) tek otomatik tetik olarak korunur. Bu uç, batch penceresini
    /// beklemeden CompetitionContext'i api-football'dan (fixtures?id → league.type/round) doldurmak
    /// içindir. Her fixture = 1 api-football isteği; maxPerRun kota korumasıdır.
    /// </summary>
    [ApiController]
    [Route("admin/competition-context")]
    public class AdminCompetitionContextController : ControllerBase
    {
        private readonly WorldPerceptionDailyJob _job;

        public AdminCompetitionContextController(WorldPerceptionDailyJob job)
        {
            _job = job;
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromQuery] int maxPerRun = 120, CancellationToken ct = default)
        {
            var written = await _job.RefreshCompetitionContextsAsync(maxPerRun, ct);
            return Ok(new { message = "competition context refresh triggered", maxPerRun, written });
        }
    }
}
