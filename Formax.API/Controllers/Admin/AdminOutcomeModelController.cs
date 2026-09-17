using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.MatchAnalysis;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// OLASI SONUÇ MODELİ — teşhis. Son koşunun backtest/kalibrasyon/lig sınavı raporu, yaklaşan maç snapshot denetimi, snapshot
    /// geçmişi (değişim denetimi), yenileme kuyruğu, canlı karne ve analiz–kart tutarlılığı. POST uçları normal hattın bir turunu çalıştırır.
    /// </summary>
    [ApiController]
    [Route("admin/outcome-model")]
    public sealed class AdminOutcomeModelController : ControllerBase
    {
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
        private readonly FormaxDbContext _db;
        public AdminOutcomeModelController(FormaxDbContext db) => _db = db;

        [HttpGet("report")]
        public async Task<IActionResult> Report(CancellationToken ct)
        {
            var run = await _db.PredictionModelRuns.AsNoTracking().OrderByDescending(r => r.CompletedAtUtc).FirstOrDefaultAsync(ct);
            if (run == null) return Ok(new { status = "NoRun" });
            return Content("{\"runId\":\"" + run.RunId + "\",\"modelVersion\":\"" + run.ModelVersion + "\",\"status\":\"" + run.Status + "\",\"completedAtUtc\":\"" + run.CompletedAtUtc.ToString("O")
                           + "Z\",\"report\":" + run.MetricsJson + "}", "application/json");
        }

        /// <summary>Lig bazlı uygunluk (son koşu) — Enabled/Limited/Disabled, test maçı, log loss, CI, ECE, sapmalar.</summary>
        [HttpGet("eligibility")]
        public async Task<IActionResult> Eligibility(CancellationToken ct)
        {
            var run = await _db.PredictionModelRuns.AsNoTracking().Where(r => r.ModelVersion == OutcomeModelVersion.Current)
                .OrderByDescending(r => r.CompletedAtUtc).FirstOrDefaultAsync(ct);
            if (run == null) return Ok(new { status = "NoRun" });
            var rows = await _db.LeaguePredictionEligibilities.AsNoTracking().Where(e => e.RunId == run.RunId).OrderBy(e => e.LeagueId).ToListAsync(ct);
            return Ok(new
            {
                run.RunId, run.ModelVersion, policy = EligibilityPolicy.Version, run.CompletedAtUtc,
                enabled = rows.Where(r => r.Status == PredictionEligibilities.Enabled).Select(r => r.LeagueId),
                limited = rows.Where(r => r.Status == PredictionEligibilities.Limited).Select(r => r.LeagueId),
                disabled = rows.Where(r => r.Status == PredictionEligibilities.Disabled).Select(r => r.LeagueId),
                leagues = rows.Select(r => new { r.LeagueId, r.Status, reasons = JsonSerializer.Deserialize<List<string>>(r.ReasonsJson), metrics = JsonSerializer.Deserialize<GroupMetrics>(r.MetricsJson) })
            });
        }

        /// <summary>
        /// Yaklaşan maçların GÜNCEL snapshot denetimi: uygunluk dağılımı, Enabled ana sonuç kartında Ev/Beraberlik/Deplasman, 1X/X2/12
        /// sayısı, ligler arası kapı sayısı, tekrar eden gerekçe, NeedsReview ve analiz–kart çelişkisi.
        /// </summary>
        [HttpGet("audit")]
        public async Task<IActionResult> Audit([FromServices] IMatchAnalysisReader analysis, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var rows = await (from s in _db.MatchPredictionSnapshots.AsNoTracking()
                              join m in _db.Matches.AsNoTracking() on s.MatchId equals m.Id
                              where s.IsCurrent && m.MatchDate > now && (m.Status == MatchStatuses.NotStarted || m.Status == MatchStatuses.PreMatch)
                              select new { s.MatchId, s.SnapshotId, s.PayloadJson, s.ModelVersion, s.TriggerType, m.LeagueId, m.MatchDate, Home = m.HomeTeam!.Name, Away = m.AwayTeam!.Name })
                .ToListAsync(ct);
            var parsed = rows.Select(r => (r, dto: JsonSerializer.Deserialize<OutcomeSnapshotDto>(r.PayloadJson)!)).ToList();
            var current = parsed.Where(x => x.r.ModelVersion == OutcomeModelVersion.Current).ToList();
            var enabled = current.Where(x => x.dto.PredictionEligibility == PredictionEligibilities.Enabled).ToList();
            var cards = enabled.SelectMany(x => x.dto.MainCards).ToList();
            var resultCards = enabled.Select(x => x.dto.MainCards.FirstOrDefault(c => c.Family == OutcomeFamilies.Result)).Where(c => c != null).ToList();
            var triples = enabled.GroupBy(x => string.Join(" | ", x.dto.MainCards.Select(c => c.Market))).OrderByDescending(g => g.Count()).ToList();
            var reasons = cards.Where(c => c.Reason != null).GroupBy(c => c.Reason!).Where(g => g.Count() > 1).ToList();

            var contradictions = new List<object>();
            foreach (var x in current)
            {
                var a = await analysis.GetAsync(x.r.MatchId, ct);
                if (a.RemovedSentences > 0) contradictions.Add(new { x.r.MatchId, removed = a.RemovedSentences, a.SnapshotId });
            }
            var needsReview = await _db.MatchPredictionSnapshots.AsNoTracking().CountAsync(s => s.PublicationStatus == "NeedsReview" && s.KickoffUtc > now, ct);

            return Ok(new
            {
                upcomingWithSnapshot = parsed.Count,
                currentModelSnapshots = current.Count,
                olderModelSnapshots = parsed.Count - current.Count,
                eligibility = current.GroupBy(x => x.dto.PredictionEligibility).ToDictionary(g => g.Key, g => g.Count()),
                eligibilityByLeague = current.GroupBy(x => x.r.LeagueId).ToDictionary(g => g.Key.ToString(), g => g.GroupBy(x => x.dto.PredictionEligibility).ToDictionary(k => k.Key, k => k.Count())),
                eligibilityReasons = current.SelectMany(x => x.dto.EligibilityReasons).GroupBy(r => r).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                crossLeagueMatches = current.Count(x => x.dto.Strength?.CrossLeague == true),
                crossLeagueGated = current.Count(x => x.dto.Strength?.CrossLeague == true && x.dto.PredictionEligibility != PredictionEligibilities.Enabled),
                crossLeagueOutliers = current.Count(x => x.dto.EligibilityReasons.Contains("OUTLIER_PROBABILITY") && x.dto.Strength?.CrossLeague == true),
                outlierGated = current.Count(x => x.dto.EligibilityReasons.Contains("OUTLIER_PROBABILITY")),
                ratingConflictGated = current.Count(x => x.dto.EligibilityReasons.Contains("RATING_DIRECTION_CONFLICT")),
                enabledResultCards = new
                {
                    home = resultCards.Count(c => c!.MarketKey == OddsMarketKeys.Ms1),
                    draw = resultCards.Count(c => c!.MarketKey == OddsMarketKeys.MsX),
                    away = resultCards.Count(c => c!.MarketKey == OddsMarketKeys.Ms2)
                },
                mainCardCount = cards.Count,
                mainCards1X = cards.Count(c => c.MarketKey == OddsMarketKeys.DoubleChance1X),
                mainCardsX2 = cards.Count(c => c.MarketKey == OddsMarketKeys.DoubleChanceX2),
                mainCards12 = cards.Count(c => c.MarketKey == OddsMarketKeys.DoubleChance12),
                allThreeFamiliesDistinct = enabled.All(x => x.dto.MainCards.Select(c => c.Family).Distinct().Count() == 3),
                marketDistribution = cards.GroupBy(c => c.Market).ToDictionary(g => g.Key, g => g.Count()),
                distinctTriples = triples.Count,
                mostRepeatedTriple = triples.FirstOrDefault()?.Key,
                mostRepeatedTripleCount = triples.FirstOrDefault()?.Count() ?? 0,
                allConsistent = enabled.All(x => x.dto.Checks?.Consistent == true),
                repeatedReasonTexts = reasons.Count,
                repeatedReasonMax = reasons.Select(g => g.Count()).DefaultIfEmpty(0).Max(),
                analysisContradictionMatches = contradictions.Count,
                analysisContradictions = contradictions,
                needsReviewUpcoming = needsReview,
                samples = current.OrderBy(x => x.r.MatchDate).Take(80).Select(x => new
                {
                    x.r.MatchId, x.r.SnapshotId, x.r.LeagueId, kickoffUtc = x.r.MatchDate, match = x.r.Home + " - " + x.r.Away, x.r.TriggerType,
                    x.dto.PredictionEligibility, x.dto.EligibilityReasons, x.dto.Strength,
                    expected = new { x.dto.ExpectedHomeGoals, x.dto.ExpectedAwayGoals }, x.dto.EvidenceCoverage,
                    cards = x.dto.MainCards.Select(c => new { c.Family, c.Market, c.Probability, c.BaselineProbability, c.InformationLift, c.SelectionScore, c.ReasonCodes, c.Reason }),
                    result = x.dto.Families.FirstOrDefault(f => f.Family == OutcomeFamilies.Result)?.Items.Select(i => new { i.Market, i.Probability }),
                    x.dto.Checks
                })
            });
        }

        /// <summary>Bir maçın bütün snapshot'ları (yayımlanmış + NeedsReview) ve değişim denetimi.</summary>
        [HttpGet("snapshots/{matchId:int}")]
        public async Task<IActionResult> History(int matchId, CancellationToken ct)
        {
            var rows = await _db.MatchPredictionSnapshots.AsNoTracking().Where(s => s.MatchId == matchId).OrderBy(s => s.ComputedAtUtc).ToListAsync(ct);
            return Ok(rows.Select(s =>
            {
                var dto = JsonSerializer.Deserialize<OutcomeSnapshotDto>(s.PayloadJson);
                return new
                {
                    s.SnapshotId, s.ModelVersion, s.CalibrationRunId, s.ComputedAtUtc, s.IsCurrent, s.PublicationStatus, s.PredictionEligibility,
                    s.PreviousSnapshotId, s.TriggerType, s.TriggerSource, s.TriggeredAtUtc, s.IntelligenceFingerprint, s.KickoffUtc,
                    eligibilityReasons = dto?.EligibilityReasons, strength = dto?.Strength,
                    result = dto?.Families.FirstOrDefault(f => f.Family == OutcomeFamilies.Result)?.Items.Select(i => new { i.Market, i.Probability, i.CalibratedProbability }),
                    mainCards = dto?.MainCards.Select(c => new { c.Market, c.Probability }),
                    changeAudit = s.ChangeAuditJson == null ? null : JsonSerializer.Deserialize<OutcomeChangeAudit>(s.ChangeAuditJson)
                };
            }));
        }

        [HttpGet("queue")]
        public async Task<IActionResult> Queue(CancellationToken ct)
            => Ok(await _db.PredictionRecomputeRequests.AsNoTracking().OrderByDescending(r => r.RequestedAtUtc).Take(200).ToListAsync(ct));

        [HttpPost("queue/run")]
        public async Task<IActionResult> RunQueue([FromServices] PredictionRecomputeWorker worker, CancellationToken ct)
            => Ok(await worker.RunOnceAsync(DateTime.UtcNow, ct));

        /// <summary>Canlı tahmin karnesi — kilitlenen snapshot'lar ve sonuç botunun kanonik sonucuyla değerlendirme.</summary>
        [HttpGet("scorecard")]
        public async Task<IActionResult> Scorecard(CancellationToken ct)
        {
            var rows = await _db.PredictionScorecards.AsNoTracking().OrderByDescending(s => s.KickoffUtc).Take(500).ToListAsync(ct);
            var settled = rows.Where(r => r.SettledAtUtc != null && r.FinalStatus == MatchStatuses.Finished).ToList();
            object Summary(IEnumerable<Formax.Domain.Entities.PredictionScorecard> xs)
            {
                var l = xs.ToList();
                return new
                {
                    matches = l.Count,
                    resultCardHitRate = l.Count(x => x.ResultCardCorrect != null) == 0 ? (double?)null : Math.Round(l.Count(x => x.ResultCardCorrect == true) / (double)l.Count(x => x.ResultCardCorrect != null), 4),
                    goalsCardHitRate = l.Count(x => x.GoalsCardCorrect != null) == 0 ? (double?)null : Math.Round(l.Count(x => x.GoalsCardCorrect == true) / (double)l.Count(x => x.GoalsCardCorrect != null), 4),
                    bttsCardHitRate = l.Count(x => x.BttsCardCorrect != null) == 0 ? (double?)null : Math.Round(l.Count(x => x.BttsCardCorrect == true) / (double)l.Count(x => x.BttsCardCorrect != null), 4),
                    meanResultLogLoss = l.Count(x => x.ResultLogLoss != null) == 0 ? (double?)null : Math.Round(l.Where(x => x.ResultLogLoss != null).Average(x => x.ResultLogLoss!.Value), 4)
                };
            }
            return Ok(new
            {
                locked = rows.Count,
                settled = settled.Count,
                all = Summary(settled),
                enabledOnly = Summary(settled.Where(s => s.Eligibility == PredictionEligibilities.Enabled)),
                byEligibility = rows.GroupBy(r => r.Eligibility).ToDictionary(g => g.Key, g => g.Count()),
                rows
            });
        }

        [HttpGet("diagnostics")]
        public async Task<IActionResult> Diagnostics(CancellationToken ct)
            => Ok(await _db.PredictionDiagnostics.AsNoTracking().OrderByDescending(d => d.CreatedAtUtc).Take(200).ToListAsync(ct));

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
