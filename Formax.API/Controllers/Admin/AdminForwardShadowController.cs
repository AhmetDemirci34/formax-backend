using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// İLERİYE DÖNÜK GÖLGE YARIŞI (Model 4.0 vs Model 5 gölge vs Model 6 gölge [yalnız 1X2]) — SALT OKUNUR. Kickoff öncesi kilitli kayıtlar ve otomatik puanlama
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
                .Select(r => new { r.MatchId, r.LeagueId, r.ModelVersion, r.Market, r.SignalTier, r.SelectedProbability, r.Correct, r.LogLoss, r.Brier, r.ScoredAtUtc, r.ActualOutcome, r.KickoffUtc, r.PredictionLockedAtUtc, r.ProbabilitiesJson })
                .ToListAsync(ct);
            var scored = rows.Where(r => r.ScoredAtUtc != null && r.ActualOutcome != "Void").ToList();
            // Eşli karşılaştırma: gölge model ile 4.0'ın birlikte puanlandığı maç × market.
            var paired = scored.GroupBy(r => (r.MatchId, r.Market))
                .SelectMany(g => new[] { Model5Shadow.Version, Model6Shadow.Version }
                    .Where(v => g.Any(x => x.ModelVersion == v) && g.Any(x => x.ModelVersion == OutcomeModelVersion.Current))
                    .Select(v => new { Model = v, g.Key.Market, D = g.First(x => x.ModelVersion == v).LogLoss!.Value - g.First(x => x.ModelVersion == OutcomeModelVersion.Current).LogLoss!.Value }))
                .ToList();
            // Model 6 ileriye dönük üretim kapısı (yalnız rapor; üretimi değiştirmez): aynı maçta kilitlenmiş 4.0 ve Model 6 1X2.
            static Probs3 P3(string json) { var d = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, double>>(json)!; return Probs3.Normalized(d["1"], d["X"], d["2"]); }
            var m6Pairs = scored.Where(r => r.Market == MarketFamilies.MatchResult).GroupBy(r => r.MatchId)
                .Where(g => g.Any(x => x.ModelVersion == Model6Shadow.Version) && g.Any(x => x.ModelVersion == OutcomeModelVersion.Current))
                .Select(g =>
                {
                    var a = g.First(x => x.ModelVersion == OutcomeModelVersion.Current); var b = g.First(x => x.ModelVersion == Model6Shadow.Version);
                    return new Model6Shadow.ForwardPair(g.Key, a.LeagueId, a.KickoffUtc, P3(a.ProbabilitiesJson), P3(b.ProbabilitiesJson), a.ActualOutcome == "1" ? 0 : a.ActualOutcome == "X" ? 1 : 2);
                }).ToList();
            var gate = Model6Shadow.ForwardGate(m6Pairs);
            return Ok(new
            {
                models = new[] { OutcomeModelVersion.Current, Model5Shadow.Version, Model6Shadow.Version },
                model5ConfigHash = Model5Shadow.ConfigHash,
                model6ConfigHash = Model6Shadow.ConfigHash,
                model6Markets = ForwardPredictionLedger.Model6Markets,
                model6ForwardGate = new
                {
                    gate.Status, gate.Matches, gate.Weeks, perOrganization = gate.PerOrganization, gate.Reasons,
                    logLoss40 = gate.LogLoss40, logLoss6 = gate.LogLoss6, diff = new { mean = gate.LogLossDiff.Mean, low = gate.LogLossDiff.Low, high = gate.LogLossDiff.High },
                    brier40 = gate.Brier40, brier6 = gate.Brier6, ece40 = gate.Ece40, ece6 = gate.Ece6, accuracy40 = gate.Accuracy40, accuracy6 = gate.Accuracy6,
                    requirement = new { minMatches = Model6Shadow.MinForwardMatches, minWeeks = Model6Shadow.MinForwardWeeks, minPerOrganization = Model6Shadow.MinPerOrganization }
                },
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
                pairedLogLossDiff = paired.GroupBy(p => (p.Model, p.Market)).Select(g => new { model = g.Key.Model, market = g.Key.Market, n = g.Count(), meanDiffMinus40 = Math.Round(g.Average(x => x.D), 5) })
            });
        }
    }
}
