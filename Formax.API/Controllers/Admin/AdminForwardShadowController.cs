using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;
using Formax.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// İLERİYE DÖNÜK GÖLGE YARIŞI (Model 4.0 vs Model 5 gölge) — SALT OKUNUR. Kickoff öncesi kilitli kayıtlar ve otomatik puanlama
    /// özeti. Yalnız DB okur; model eğitimi, değerlendirme ya da dış istek tetiklemez. Kullanıcı uçları bu veriyi okumaz.
    /// </summary>
    [ApiController]
    [Route("admin/forward-shadow")]
    public sealed class AdminForwardShadowController : ControllerBase
    {
        private readonly FormaxDbContext _db;
        public AdminForwardShadowController(FormaxDbContext db) => _db = db;

        private static object W(int c, int n) { var (lo, hi) = SelectivePrediction.Wilson(c, n); return new { low = Math.Round(lo, 4), high = Math.Round(hi, 4) }; }

        [HttpGet("")]
        public async Task<IActionResult> Summary(CancellationToken ct)
        {
            var rows = await _db.ForwardPredictionRecords.AsNoTracking()
                .Select(r => new { r.MatchId, r.ModelVersion, r.Market, r.SignalTier, r.SelectedProbability, r.Correct, r.LogLoss, r.Brier, r.ScoredAtUtc, r.ActualOutcome, r.KickoffUtc, r.PredictionLockedAtUtc })
                .ToListAsync(ct);
            var scored = rows.Where(r => r.ScoredAtUtc != null && r.ActualOutcome != "Void").ToList();
            // Eşli karşılaştırma: iki modelin de puanlandığı maç × market.
            var paired = scored.GroupBy(r => (r.MatchId, r.Market)).Where(g => g.Select(x => x.ModelVersion).Distinct().Count() == 2)
                .Select(g => new
                {
                    g.Key.Market,
                    D = g.First(x => x.ModelVersion == Model5Shadow.Version).LogLoss!.Value - g.First(x => x.ModelVersion == OutcomeModelVersion.Current).LogLoss!.Value
                }).ToList();
            return Ok(new
            {
                models = new[] { OutcomeModelVersion.Current, Model5Shadow.Version },
                model5ConfigHash = Model5Shadow.ConfigHash,
                selector = SelectivePrediction.ForwardSelectorVersion,
                lockWindowHours = 24,
                recorded = rows.Count,
                matches = rows.Select(r => r.MatchId).Distinct().Count(),
                lockedBeforeKickoff = rows.All(r => r.PredictionLockedAtUtc < r.KickoffUtc),
                scored = scored.Count,
                pending = rows.Count(r => r.ScoredAtUtc == null),
                byModelMarket = scored.GroupBy(r => (r.ModelVersion, r.Market)).OrderBy(g => g.Key.ModelVersion).ThenBy(g => g.Key.Market).Select(g => new
                {
                    model = g.Key.ModelVersion, market = g.Key.Market, n = g.Count(),
                    accuracy = Math.Round(g.Average(x => x.Correct == true ? 1.0 : 0.0), 4),
                    meanProbability = Math.Round(g.Average(x => x.SelectedProbability), 4),
                    logLoss = Math.Round(g.Average(x => x.LogLoss ?? 0), 5), brier = Math.Round(g.Average(x => x.Brier ?? 0), 5),
                    wilson = W(g.Count(x => x.Correct == true), g.Count())
                }),
                strongest = scored.Where(r => r.SignalTier == SelectivePrediction.Tiers.Strongest).GroupBy(r => r.ModelVersion).Select(g => new
                {
                    model = g.Key, n = g.Count(), correct = g.Count(x => x.Correct == true),
                    wilson = W(g.Count(x => x.Correct == true), g.Count()),
                    note = g.Count() < 200 ? "Yetersiz örneklem — başarı iddiası yapılmaz (en az 200 gerekir)" : null
                }),
                pairedLogLossDiff = paired.GroupBy(p => p.Market).Select(g => new { market = g.Key, n = g.Count(), meanDiffModel5Minus40 = Math.Round(g.Average(x => x.D), 5) })
            });
        }
    }
}
