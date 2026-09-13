using Formax.Application.Interfaces;
using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// Kadro alımının tek maçlık manuel tetiği. Otomatik tetik LineupIngestionJob'un kendi
    /// döngüsüdür (T−60 … kickoff+10, resmî kaynak); burası doğrulama ve geri-doldurma içindir.
    /// API-Football'a ÇIKMAZ: aynı resmî okuma/doğrulama/yazma yolu çalışır.
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

        /// <summary>Tek maç için resmî kaynaktan kadro doğrular ve (varsa) yazar.</summary>
        [HttpPost("sync")]
        public async Task<IActionResult> Sync([FromQuery] int matchId, CancellationToken ct)
        {
            var outcome = await _job.RunForMatchAsync(matchId, ct);
            if (outcome.Outcome == "MatchNotFound") return NotFound(new { error = "Match not found", matchId });

            var header = _lineupRepo.GetByMatchId(matchId);
            var players = _lineupRepo.GetPlayersByMatchId(matchId);

            return Ok(new
            {
                matchId,
                outcome = outcome.Outcome,
                source = outcome.SourceKey,
                detail = outcome.Detail,
                provider = header?.Provider,
                sourceUrl = header?.SourceUrl,
                verificationStatus = header?.VerificationStatus,
                verifiedAtUtc = header?.VerifiedAtUtc,
                homeReleased = header?.HomeLineupsReleased,
                awayReleased = header?.AwayLineupsReleased,
                homeFormation = header?.HomeFormation,
                awayFormation = header?.AwayFormation,
                homeStarters = players.Count(p => p.Side == "Home" && p.Role == "Starter"),
                awayStarters = players.Count(p => p.Side == "Away" && p.Role == "Starter"),
                players = players.Count
            });
        }
    }
}
