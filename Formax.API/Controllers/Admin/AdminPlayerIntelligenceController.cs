using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// Football Intelligence v1.0 — player/squad ingestion manuel tetikleyici (test/kanıt).
    /// 12h zamanlı PlayerIntelligenceSyncJob tek otomatik tetikleyicidir.
    /// </summary>
    [ApiController]
    [Route("admin/player-intelligence")]
    public class AdminPlayerIntelligenceController : ControllerBase
    {
        private readonly PlayerIntelligenceIngestionService _svc;

        public AdminPlayerIntelligenceController(PlayerIntelligenceIngestionService svc) => _svc = svc;

        /// <summary>Tek takım için ingest (external api-football takım id).</summary>
        [HttpPost("run-team")]
        public async Task<IActionResult> RunTeam([FromQuery] string externalTeamId, [FromQuery] int? season, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(externalTeamId))
                return BadRequest(new { error = "externalTeamId is required" });
            var hasData = await _svc.IngestByExternalAsync(externalTeamId, season, ct);
            return Ok(new { externalTeamId, hasData });
        }

        /// <summary>Yaklaşan maçlardaki takımlar için ingest.</summary>
        [HttpPost("run-upcoming")]
        public async Task<IActionResult> RunUpcoming([FromQuery] int max = 60, CancellationToken ct = default)
        {
            var withData = await _svc.IngestUpcomingAsync(max, ct);
            return Ok(new { withData });
        }
    }
}
