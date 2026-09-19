using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Lineups;
using Formax.Application.Services.Outcomes;
using Formax.Infrastructure.Lineups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// KADRO/OYUNCU ETKİ KATMANI — teşhis ve ölçüm.
    ///
    /// Bu uçların hiçbiri API-Football'a ÇIKMAZ ve hiçbir hücreyi kendiliğinden AÇMAZ: ölçüm raporu
    /// üretirler, üretime geçiş <c>LineupImpact:Production</c> ayarının açılmasına bağlıdır.
    /// </summary>
    [ApiController]
    [Route("admin/lineup-impact")]
    public sealed class AdminLineupImpactController : ControllerBase
    {
        private readonly LineupImpactBacktestService _backtest;
        private readonly LineupHistoryLoader _lineups;
        private readonly IConfiguration _config;

        public AdminLineupImpactController(LineupImpactBacktestService backtest, LineupHistoryLoader lineups, IConfiguration config)
        {
            _backtest = backtest; _lineups = lineups; _config = config;
        }

        /// <summary>Katmanın anlık durumu — sürüm, üretim anahtarı, kadro kapsamı.</summary>
        [HttpGet("status")]
        public async Task<IActionResult> Status(CancellationToken ct)
        {
            var official = await _lineups.LoadAsync(null, officialOnly: true, ct);
            var all = await _lineups.LoadAsync(null, officialOnly: false, ct);
            var verified = official.Count(l => LineupVerificationRule.Check(l).Accepted);
            return Ok(new
            {
                impactVersion = LineupImpactVersion.Current,
                policyVersion = LineupImpactPolicy.Version,
                production = _config.GetValue("LineupImpact:Production", false),
                officialObservations = official.Count,
                officialVerified = verified,
                allObservations = all.Count,
                byLeague = official.GroupBy(l => l.LeagueId)
                    .Select(g => new { leagueId = g.Key, observations = g.Count(), verified = g.Count(x => LineupVerificationRule.Check(x).Accepted) })
                    .OrderBy(x => x.leagueId)
            });
        }

        /// <summary>
        /// ZAMANSAL ÖLÇÜM — taban model, 6 ablasyon ve 11 × market ailesi matrisi. Uzun sürer (dakikalar).
        /// </summary>
        [HttpPost("backtest")]
        public async Task<IActionResult> Backtest([FromQuery] bool officialOnly = true, [FromQuery] int? testDays = null, CancellationToken ct = default)
        {
            var window = testDays.HasValue ? TimeSpan.FromDays(testDays.Value) : (TimeSpan?)null;
            var report = await _backtest.RunAsync(DateTime.UtcNow, officialOnly, window, ct);
            return Ok(report);
        }

        /// <summary>Ölçüm özeti — matrisin tamamı yerine karar ve hücre değişimi.</summary>
        [HttpPost("backtest/summary")]
        public async Task<IActionResult> Summary([FromQuery] bool officialOnly = true, [FromQuery] int? testDays = null, CancellationToken ct = default)
        {
            var window = testDays.HasValue ? TimeSpan.FromDays(testDays.Value) : (TimeSpan?)null;
            var r = await _backtest.RunAsync(DateTime.UtcNow, officialOnly, window, ct);
            return Ok(new
            {
                r.ImpactVersion, r.ModelVersion, r.Decision, r.DecisionReasons, r.OpenedCells,
                r.HistoryMatches, r.LineupObservations, r.VerifiedObservations,
                r.TestMatches, r.TestMatchesWithLineup, r.LineupCoverage,
                r.ResolvedPlayers, r.SufficientPlayers, r.StartsPerPlayer,
                baseline = r.BaseOverall.Select(m => new { m.Family, m.Matches, m.LogLoss, m.Brier, m.CalibrationError, m.Status }),
                ablations = r.Ablations.Select(a => new
                {
                    a.Name, a.Description, a.AdjustedMatches, a.MeanAbsDelta, a.MaxAbsDelta,
                    overall = a.Overall.Select(m => new { m.Family, m.LogLoss, m.Brier, m.CalibrationError, m.Status }),
                    adjustedOnly = a.AdjustedOnlyLogLossDiff.ToDictionary(
                        k => k.Key,
                        k => new { diff = k.Value, ciLow = a.AdjustedOnlyCiLow.GetValueOrDefault(k.Key), ciHigh = a.AdjustedOnlyCiHigh.GetValueOrDefault(k.Key) })
                }),
                matrixBefore = Matrix(r.BaseByLeague),
                matrixAfter = Matrix(r.Ablations.First(a => a.Name == "A6_Candidate").ByLeague)
            });
        }

        private static object Matrix(System.Collections.Generic.IReadOnlyList<MarketFamilyMetrics> rows)
            => rows.GroupBy(x => x.LeagueId).OrderBy(g => g.Key).Select(g => new
            {
                leagueId = g.Key,
                families = g.OrderBy(x => x.Family).Select(x => new { x.Family, x.Status, x.Matches, x.LogLoss, x.BaselineLogLoss, x.LogLossDiffCiHigh, x.CalibrationError })
            });
    }
}
