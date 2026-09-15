using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// OLASI SONUÇ MODELİ — teşhis. Son koşunun backtest/kalibrasyon raporu ve yaklaşan maç snapshot'larının ana kart denetimi.
    /// POST uçları normal arka plan hattının bir turunu çalıştırır.
    /// </summary>
    [ApiController]
    [Route("admin/outcome-model")]
    public sealed class AdminOutcomeModelController : ControllerBase
    {
        private readonly FormaxDbContext _db;
        public AdminOutcomeModelController(FormaxDbContext db) => _db = db;

        [HttpGet("report")]
        public async Task<IActionResult> Report(CancellationToken ct)
        {
            var run = await _db.PredictionModelRuns.AsNoTracking().OrderByDescending(r => r.CompletedAtUtc).FirstOrDefaultAsync(ct);
            if (run == null) return Ok(new { status = "NoRun" });
            return Content("{\"runId\":\"" + run.RunId + "\",\"status\":\"" + run.Status + "\",\"completedAtUtc\":\"" + run.CompletedAtUtc.ToString("O")
                           + "Z\",\"report\":" + run.MetricsJson + "}", "application/json");
        }

        /// <summary>Yaklaşan maçların GÜNCEL snapshot'larında ana kart denetimi (1X/X2/12 sayısı, aile dağılımı, tekrar eden üçlü, tutarlılık).</summary>
        [HttpGet("audit")]
        public async Task<IActionResult> Audit(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var rows = await (from s in _db.MatchPredictionSnapshots.AsNoTracking()
                              join m in _db.Matches.AsNoTracking() on s.MatchId equals m.Id
                              where s.IsCurrent && m.MatchDate > now && (m.Status == MatchStatuses.NotStarted || m.Status == MatchStatuses.PreMatch)
                              select new { s.MatchId, s.SnapshotId, s.PayloadJson, m.LeagueId, m.MatchDate, Home = m.HomeTeam!.Name, Away = m.AwayTeam!.Name })
                .ToListAsync(ct);
            var parsed = rows.Select(r => (r, dto: JsonSerializer.Deserialize<OutcomeSnapshotDto>(r.PayloadJson)!)).ToList();
            var available = parsed.Where(x => x.dto.Status == "Available").ToList();
            var cards = available.SelectMany(x => x.dto.MainCards).ToList();
            var triples = available.GroupBy(x => string.Join(" | ", x.dto.MainCards.Select(c => c.Market))).OrderByDescending(g => g.Count()).ToList();
            return Ok(new
            {
                upcomingWithSnapshot = parsed.Count,
                available = available.Count,
                insufficientData = parsed.Count(x => x.dto.Status == "InsufficientData"),
                leagues = available.GroupBy(x => x.r.LeagueId).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                mainCardCount = cards.Count,
                mainCards1X = cards.Count(c => c.MarketKey == OddsMarketKeys.DoubleChance1X),
                mainCardsX2 = cards.Count(c => c.MarketKey == OddsMarketKeys.DoubleChanceX2),
                mainCards12 = cards.Count(c => c.MarketKey == OddsMarketKeys.DoubleChance12),
                allThreeFamiliesDistinct = available.All(x => x.dto.MainCards.Select(c => c.Family).Distinct().Count() == 3),
                familyDistribution = cards.GroupBy(c => c.Family).ToDictionary(g => g.Key, g => g.Count()),
                marketDistribution = cards.GroupBy(c => c.Market).ToDictionary(g => g.Key, g => g.Count()),
                distinctTriples = triples.Count,
                mostRepeatedTriple = triples.FirstOrDefault()?.Key,
                mostRepeatedTripleCount = triples.FirstOrDefault()?.Count() ?? 0,
                allConsistent = available.All(x => x.dto.Checks?.Consistent == true),
                distinctReasonTexts = cards.Select(c => c.Reason).Distinct().Count(),
                samples = available.OrderBy(x => x.r.MatchDate).Take(60).Select(x => new
                {
                    x.r.MatchId, x.r.SnapshotId, x.r.LeagueId, kickoffUtc = x.r.MatchDate, match = x.r.Home + " - " + x.r.Away,
                    expected = new { x.dto.ExpectedHomeGoals, x.dto.ExpectedAwayGoals }, x.dto.EvidenceCoverage,
                    cards = x.dto.MainCards.Select(c => new { c.Family, c.Market, c.Probability, c.BaselineProbability, c.InformationLift, c.SelectionScore, c.ReasonCodes, c.Reason }),
                    result = x.dto.Families.First(f => f.Family == OutcomeFamilies.Result).Items.Select(i => new { i.Market, i.Probability }),
                    x.dto.Checks
                })
            });
        }

        [HttpPost("train")]
        public async Task<IActionResult> Train([FromServices] OutcomeModelTrainingService svc, CancellationToken ct)
        {
            var run = await svc.RunAsync(DateTime.UtcNow, ct);
            return Ok(new { run.RunId, run.Status, run.TrainMatches, run.CalibrationMatches, run.TestMatches });
        }

        [HttpPost("snapshots/run")]
        public async Task<IActionResult> Snapshots([FromServices] MatchPredictionSnapshotService svc, CancellationToken ct)
            => Ok(await svc.RunAsync(DateTime.UtcNow, ct));
    }
}
