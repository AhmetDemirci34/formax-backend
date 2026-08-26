using Formax.Application.Interfaces;
using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// Kadro ingestion'ının tek maçlık manuel tetiği + kapsam teşhisi.
    /// Otomatik tetik LineupIngestionJob'un kendi döngüsüdür (kickoff'a &lt;=60 dk);
    /// burası doğrulama ve şema genişlemesi sonrası geri-doldurma içindir.
    /// </summary>
    [ApiController]
    [Route("admin/lineup")]
    public class AdminLineupController : ControllerBase
    {
        private readonly LineupIngestionJob _job;
        private readonly IMatchLineupRepository _lineupRepo;

        public AdminLineupController(LineupIngestionJob job, IMatchLineupRepository lineupRepo)
        {
            _job = job;
            _lineupRepo = lineupRepo;
        }

        /// <summary>Tek maç için sağlayıcıdan kadro çeker (formation + grid dahil).</summary>
        [HttpPost("sync")]
        public async Task<IActionResult> Sync([FromQuery] int matchId, CancellationToken ct)
        {
            var ok = await _job.RunForMatchAsync(matchId, ct);
            if (!ok) return NotFound(new { error = "Match not found or not mapped", matchId });

            var header = _lineupRepo.GetByMatchId(matchId);
            var players = _lineupRepo.GetPlayersByMatchId(matchId);

            return Ok(new
            {
                matchId,
                homeFormation = header?.HomeFormation,
                awayFormation = header?.AwayFormation,
                players = players.Count,
                withGrid = players.Count(p => !string.IsNullOrWhiteSpace(p.Grid))
            });
        }
    }
}
