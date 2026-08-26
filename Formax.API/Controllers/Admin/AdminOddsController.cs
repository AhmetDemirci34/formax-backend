using Formax.Application.Interfaces;
using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// GERÇEK market oranı ingestion'ının manuel tetiği + kapsam teşhisi.
    /// Otomatik tetik OddsIngestionJob'un kendi döngüsüdür; burası doğrulama içindir.
    /// </summary>
    [ApiController]
    [Route("admin/odds")]
    public class AdminOddsController : ControllerBase
    {
        private readonly OddsIngestionJob _job;
        private readonly IMatchOddsRepository _oddsRepo;

        public AdminOddsController(OddsIngestionJob job, IMatchOddsRepository oddsRepo)
        {
            _job = job;
            _oddsRepo = oddsRepo;
        }

        /// <summary>Tek ingestion turu çalıştırır (sağlayıcı → MatchMarketOdds).</summary>
        [HttpPost("sync")]
        public async Task<IActionResult> Sync(CancellationToken ct)
        {
            var (pages, matched, rows) = await _job.RunOnceAsync(ct);
            return Ok(new { pages, matchesMatched = matched, marketRows = rows });
        }

        /// <summary>Kaç maçın gerçek oranı var + tek maçın market dökümü.</summary>
        [HttpGet("coverage")]
        public async Task<IActionResult> Coverage([FromQuery] int? matchId, CancellationToken ct)
        {
            var matchesWithOdds = await _oddsRepo.CountMatchesWithOddsAsync(ct);

            if (matchId == null)
                return Ok(new { matchesWithOdds });

            var rows = await _oddsRepo.GetByMatchAsync(matchId.Value, ct);
            return Ok(new
            {
                matchesWithOdds,
                matchId,
                markets = rows.Select(r => new
                {
                    r.MarketKey,
                    r.Odd,
                    r.PreviousOdd,
                    r.BookmakerName,
                    r.CapturedAtUtc
                })
            });
        }
    }
}
