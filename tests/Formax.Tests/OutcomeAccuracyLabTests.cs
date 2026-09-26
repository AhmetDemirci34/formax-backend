using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Pred = Formax.Application.Services.Outcomes.OutcomeAccuracyLab.Pred;
using Axes = Formax.Application.Services.Outcomes.OutcomeBacktest.LeagueCalibrationAxes;

namespace Formax.Tests;

/// <summary>
/// TAHMİN DOĞRULUĞU — GERÇEK VERİ LABORATUVARI (26.09.2026). YALNIZ OKUR; dış istek yok. <c>FORMAX_LAB_SQL</c> yoksa atlanır.
///
/// Düzen: eğitim [kesim−900g, kesim−750g) → reyting parametreleri; validation [kesim−750g, kesim−600g) → kalibrasyon ve BÜTÜN aday
/// seçimleri; final test [kesim−600g, kesim) → seçilmiş TEK adayın tek değerlendirmesi. Reyting her maçta yalnız o maçtan önceki
/// sonuçlarla ilerler (walk-forward). Bütün karşılaştırmalar aynı MatchId kümesinde eşlidir.
/// </summary>
public class OutcomeAccuracyLabTests
{
    public static readonly DateTime Cutoff = new(2026, 9, 25, 2, 0, 0, DateTimeKind.Utc);
    private static readonly int[] Orgs = { 2, 3, 39, 40, 61, 78, 88, 135, 140, 203, 848 };
    private static readonly double[] EloC = { 0.35, 0.50, 0.65 };
    private static readonly double[] EnsembleW = { 0.10, 0.20, 0.30, 0.50 };
    private static readonly double[] RestBeta = { 0.01, 0.02, 0.03 };

    [SkippableFact]
    public async Task Lab_TahminDogrulugu_TabanlarAdaylarAblasyon()
    {
        var conn = Environment.GetEnvironmentVariable("FORMAX_LAB_SQL");
        Skip.If(string.IsNullOrWhiteSpace(conn), "FORMAX_LAB_SQL yok");
        var tag = Environment.GetEnvironmentVariable("FORMAX_LAB_TAG") ?? "run";
        await using var db = new FormaxDbContext(new DbContextOptionsBuilder<FormaxDbContext>().UseSqlServer(conn, o => o.CommandTimeout(120)).Options);
        var loader = new OutcomeHistoryLoader(db);
        var history = await loader.LoadAsync(Cutoff);
        var names = await loader.LoadCompetitionNamesAsync();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var testStart = Cutoff - OutcomeModelTrainingService.TestWindow;
        var calStart = testStart - OutcomeModelTrainingService.CalibrationWindow;
        var evalStart = calStart - OutcomeModelTrainingService.TrainWindow;
        OutcomeBacktestReport Run(Axes axes) => OutcomeBacktest.Run(history, CompetitionCatalog.Build(history, names), LockedCompetitions.All.ToHashSet(),
            evalStart, calStart, testStart, Cutoff, Cutoff, compareLegacy: false, candidate: true, leagueCalibration: axes);

        // ── A) Üretim modeli (4.0) — iki kez: determinizm ──
        var report = Run(Axes.None);
        var art = OutcomeBacktest.LastArtifacts!;
        var again = Run(Axes.None);
        var deterministic = JsonSerializer.Serialize(report.MarketEligibility) == JsonSerializer.Serialize(again.MarketEligibility)
                            && JsonSerializer.Serialize(report.Parameters) == JsonSerializer.Serialize(again.Parameters);
        var chosen = art.Chosen; var baseP = art.BaseParams;
        var test = art.LockedTestSamples.ToList();
        var val = art.CalibrationSamples.Where(s => LockedCompetitions.All.Contains(s.LeagueId)).ToList();
        var notPred = report.MarketEligibility.Where(m => m.Family == MarketFamilies.MatchResult).ToDictionary(m => m.LeagueId!.Value, m => m.NotPredicted);

        // Sızıntı yapısal kontrolleri (bölmeler)
        var trainIds = new HashSet<int>(history.Where(m => m.KickoffUtc >= evalStart && m.KickoffUtc < calStart).Select(m => m.MatchId));
        var valIds = val.Select(s => s.MatchId).ToHashSet();
        var testIds = test.Select(s => s.MatchId).ToHashSet();
        var splitsDisjoint = !trainIds.Overlaps(valIds) && !trainIds.Overlaps(testIds) && !valIds.Overlaps(testIds);
        var datesOk = val.All(s => s.KickoffUtc >= calStart && s.KickoffUtc < testStart) && test.All(s => s.KickoffUtc >= testStart && s.KickoffUtc < Cutoff);

        Pred Prod(EvalSample s) => Pred.From(OutcomePredictor.Predict(s.E, s.LeagueId, chosen).Calibrated);
        Pred Raw(EvalSample s) => Pred.From(OutcomePredictor.Predict(s.E, s.LeagueId, baseP).Raw);
        Pred Freq(EvalSample s) => Pred.FromBaseline(s);

        // ── D) Bağımsız Elo tabanı; beraberlik parametresi validation'da ──
        var elo = OutcomeAccuracyLab.EloLogits(history, Cutoff);
        double Val1X2(Func<EvalSample, Pred?> f) => OutcomeAccuracyLab.Evaluate(MarketFamilies.MatchResult, val, f).LogLoss;
        var configs = new List<object>();
        var bestC = EloC.OrderBy(c => { var l = Val1X2(s => OutcomeAccuracyLab.EloPred(elo[s.MatchId], c)); configs.Add(new { kind = "elo", c, val1x2 = l }); return l; }).First();
        Pred EloP(EvalSample s) => OutcomeAccuracyLab.EloPred(elo[s.MatchId], bestC);

        // ── E) Adaylar — YALNIZ validation seçimi; benimseme için kod tabanının mevcut parsimoni eşiği (MinimumCalibrationGain) ──
        // Ensemble skor matrisine uygulanır (sonuç sınıfı yeniden ağırlıklandırma): 1X2, çifte şans ve gol marketleri tek matristen.
        var minGain = OutcomeBacktest.MinimumCalibrationGain;
        var prodVal1x2 = Val1X2(Prod);
        ScoreDistribution Reweighted(EvalSample s, OutcomeExpectation e, Pred elo, double w)
        {
            var d = OutcomePredictor.Predict(e, s.LeagueId, chosen).Calibrated;
            return d.ReweightResult((1 - w) * d.HomeWin + w * elo.H, (1 - w) * d.Draw + w * elo.D, (1 - w) * d.AwayWin + w * elo.A);
        }
        Pred InternalElo(EvalSample s, double c)
        {
            var q = Math.Clamp(s.E.EloHomeExpectation, 1e-4, 1 - 1e-4);
            return OutcomeAccuracyLab.EloPred(Math.Log(q / (1 - q)), c);
        }
        var eloKind = "none"; var bestW = 0.0; var bestIc = 0.0; var bestWLoss = prodVal1x2;
        foreach (var w in EnsembleW)
        {
            var l = Val1X2(s => Pred.From(Reweighted(s, s.E, EloP(s), w)));
            configs.Add(new { kind = "ensemble-standalone-elo", c = bestC, w, val1x2 = l });
            if (l < bestWLoss - 1e-9) { bestWLoss = l; bestW = w; eloKind = "standalone"; }
        }
        foreach (var w in new[] { 0.2, 0.3, 0.5 })
            foreach (var c in new[] { 0.5, 0.65 })
            {
                var l = Val1X2(s => Pred.From(Reweighted(s, s.E, InternalElo(s, c), w)));
                configs.Add(new { kind = "ensemble-internal-elo", c, w, val1x2 = l });
                if (l < bestWLoss - 1e-9) { bestWLoss = l; bestW = w; bestIc = c; eloKind = "internal"; }
            }
        if (prodVal1x2 - bestWLoss < minGain) { bestW = 0; eloKind = "none"; }
        double ValCombined(Func<EvalSample, Pred?> f) => OutcomeAccuracyLab.Families.Average(fam => OutcomeAccuracyLab.Evaluate(fam, val, f).LogLoss);
        var prodValComb = ValCombined(Prod);
        var bestBeta = 0.0; var bestBetaLoss = prodValComb;
        foreach (var b in RestBeta.Concat(RestBeta.Select(x => -x)))
        {
            var l = ValCombined(s => Pred.From(OutcomePredictor.Predict(OutcomeAccuracyLab.RestAdjusted(s.E, s.KickoffUtc, b), s.LeagueId, chosen).Calibrated));
            configs.Add(new { kind = "rest", beta = b, valCombined = l });
            if (l < bestBetaLoss - 1e-9) { bestBetaLoss = l; bestBeta = b; }
        }
        if (prodValComb - bestBetaLoss < minGain) bestBeta = 0;
        Pred Cand(EvalSample s)
        {
            var e = OutcomeAccuracyLab.RestAdjusted(s.E, s.KickoffUtc, bestBeta);
            if (bestW == 0) return Pred.From(OutcomePredictor.Predict(e, s.LeagueId, chosen).Calibrated);
            var elo = eloKind == "internal" ? InternalElo(s, bestIc) : EloP(s);
            return Pred.From(Reweighted(s, e, elo, bestW));
        }
        var candidateIsProd = bestW == 0 && bestBeta == 0;

        // ── Ablasyon (validation + test; yalnız teşhis) ──
        var ablations = OutcomeAccuracyLab.AblationComponents.ToDictionary(c => c, c =>
        {
            var q = OutcomeAccuracyLab.Ablate(chosen, c);
            Pred F(EvalSample s) => Pred.From(OutcomePredictor.Predict(s.E, s.LeagueId, q).Calibrated);
            configs.Add(new { kind = "ablation", component = c });
            return new
            {
                valCombined = ValCombined(F) - prodValComb,
                test1x2 = OutcomeAccuracyLab.Evaluate(MarketFamilies.MatchResult, test, F).LogLoss - OutcomeAccuracyLab.Evaluate(MarketFamilies.MatchResult, test, Prod).LogLoss,
                testCombined = OutcomeAccuracyLab.Families.Average(fam => OutcomeAccuracyLab.Evaluate(fam, test, F).LogLoss - OutcomeAccuracyLab.Evaluate(fam, test, Prod).LogLoss)
            };
        });

        // ── Lig eksenleri — 5955d61'de test penceresinde görüldü: KABULE ADAY DEĞİL, yalnız rapor ──
        var axes = new Dictionary<string, object>();
        foreach (var ax in new[] { Axes.Draw, Axes.HomeTilt, Axes.LowScoreRho, Axes.All })
        {
            Run(ax);
            var ap = OutcomeBacktest.LastArtifacts!.Chosen;
            Pred F(EvalSample s) => Pred.From(OutcomePredictor.Predict(s.E, s.LeagueId, ap).Calibrated);
            configs.Add(new { kind = "league-axes(report-only)", axes = ax.ToString() });
            axes[ax.ToString()] = new { test1x2Diff = OutcomeAccuracyLab.Evaluate(MarketFamilies.MatchResult, test, F).LogLoss - OutcomeAccuracyLab.Evaluate(MarketFamilies.MatchResult, test, Prod).LogLoss };
        }

        // ── F) Final test — TEK aday, eşli, blok bootstrap ──
        var sb = new StringBuilder();
        sb.AppendLine($"# Tahmin doğruluğu laboratuvarı [{tag}]");
        sb.AppendLine($"kesim {Cutoff:O}; eğitim [{evalStart:yyyy-MM-dd}, {calStart:yyyy-MM-dd}) {report.TrainMatches} maç; validation [{calStart:yyyy-MM-dd}, {testStart:yyyy-MM-dd}) {val.Count} (kilitli) maç; final test [{testStart:yyyy-MM-dd}, {Cutoff:yyyy-MM-dd}) {test.Count} maç");
        sb.AppendLine($"determinizm={deterministic} bölmeler ayrık={splitsDisjoint} tarihler={datesOk} konfigürasyon sayısı={configs.Count}");
        sb.AppendLine($"seçilen: bağımsız Elo c={bestC}; ensemble tür={eloKind} w={bestW} dahiliC={bestIc} (val 1X2 {prodVal1x2:0.00000}→{bestWLoss:0.00000}, eşik {minGain}); dinlenme β={bestBeta} (val birleşik {prodValComb:0.00000}→{bestBetaLoss:0.00000}); aday=üretim? {candidateIsProd}");
        foreach (var o in Orgs)
        {
            var ss = test.Where(s => s.LeagueId == o).Select(s => OutcomeAccuracyLab.Loss(MarketFamilies.MatchResult, Cand(s), s) - OutcomeAccuracyLab.Loss(MarketFamilies.MatchResult, Prod(s), s)).DefaultIfEmpty(0).Average();
            var loo = test.Where(s => s.LeagueId != o).Select(s => OutcomeAccuracyLab.Loss(MarketFamilies.MatchResult, Cand(s), s) - OutcomeAccuracyLab.Loss(MarketFamilies.MatchResult, Prod(s), s)).Average();
            sb.AppendLine($"  1X2 lig {o}: Δ={ss:+0.00000;-0.00000}  lig hariç Δ={loo:+0.00000;-0.00000}");
        }
        var cross = test.Where(s => s.E.CrossLeague).ToList();
        sb.AppendLine($"  ligler arası maç (n={cross.Count}) 1X2 Δ={cross.Select(s => OutcomeAccuracyLab.Loss(MarketFamilies.MatchResult, Cand(s), s) - OutcomeAccuracyLab.Loss(MarketFamilies.MatchResult, Prod(s), s)).DefaultIfEmpty(0).Average():+0.00000;-0.00000}");

        var overall = new Dictionary<string, object>();
        sb.AppendLine("\n## Genel (final test)");
        sb.AppendLine("| Market | N | Frekans LL | Elo LL | 4.0 ham LL | 4.0 LL | Aday LL | 4.0 Brier | Aday Brier | 4.0 ECE | Aday ECE | Δ ort [CI] | Acc 4.0/Aday |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var fam in OutcomeAccuracyLab.Families)
        {
            var sProd = OutcomeAccuracyLab.Evaluate(fam, test, Prod);
            var sCand = OutcomeAccuracyLab.Evaluate(fam, test, Cand);
            var sFreq = OutcomeAccuracyLab.Evaluate(fam, test, Freq);
            var sRaw = OutcomeAccuracyLab.Evaluate(fam, test, Raw);
            var sElo = fam == MarketFamilies.MatchResult ? OutcomeAccuracyLab.Evaluate(fam, test, EloP) : null;
            var ci = OutcomeAccuracyLab.PairedBlockBootstrap(test.Select(s => (s.KickoffUtc, OutcomeAccuracyLab.Loss(fam, Cand(s), s) - OutcomeAccuracyLab.Loss(fam, Prod(s), s))).ToList());
            var brierCi = OutcomeAccuracyLab.PairedBlockBootstrap(test.Select(s => (s.KickoffUtc, OutcomeAccuracyLab.Brier(fam, Cand(s), s) - OutcomeAccuracyLab.Brier(fam, Prod(s), s))).ToList());
            // Tek lig anomalisi değil mi? Her lig dışarıda bırakıldığında fark hâlâ negatif mi?
            var loo = Orgs.Select(o => test.Where(s => s.LeagueId != o).Select(s => OutcomeAccuracyLab.Loss(fam, Cand(s), s) - OutcomeAccuracyLab.Loss(fam, Prod(s), s)).DefaultIfEmpty(0).Average()).ToList();
            overall[fam] = new { prod = sProd, cand = sCand, freq = sFreq, raw = sRaw, elo = sElo, diff = ci, brierDiff = brierCi, leaveOneLeagueOutMax = loo.Max() };
            sb.AppendLine($"| {fam} | {sProd.N} | {sFreq.LogLoss:0.00000} | {(sElo == null ? "–" : sElo.LogLoss.ToString("0.00000"))} | {sRaw.LogLoss:0.00000} | {sProd.LogLoss:0.00000} | {sCand.LogLoss:0.00000} | {sProd.Brier:0.00000} | {sCand.Brier:0.00000} | {sProd.Ece:0.00000} | {sCand.Ece:0.00000} | {ci.Mean:+0.00000;-0.00000} [{ci.Low:+0.00000;-0.00000}, {ci.High:+0.00000;-0.00000}] | {sProd.Accuracy:0.000}/{sCand.Accuracy:0.000} |");
        }

        // ── Hücre tablosu (11 × aile) ──
        sb.AppendLine("\n## Hücreler (final test)");
        sb.AppendLine("| Org | Market | N | Kapsam | Frekans LL | 4.0 LL | Aday LL | 4.0/Aday Brier | 4.0/Aday ECE | Δ [CI] | 4.0 kapı | Aday kapı |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        var cells = new List<object>();
        foreach (var o in Orgs)
            foreach (var fam in OutcomeAccuracyLab.Families)
            {
                var ss = test.Where(s => s.LeagueId == o).ToList();
                var np = notPred.GetValueOrDefault(o);
                var a = OutcomeAccuracyLab.Evaluate(fam, ss, Prod, np);
                var b = OutcomeAccuracyLab.Evaluate(fam, ss, Cand, np);
                var f = OutcomeAccuracyLab.Evaluate(fam, ss, Freq, np);
                var ci = OutcomeAccuracyLab.PairedBlockBootstrap(ss.Select(s => (s.KickoffUtc, OutcomeAccuracyLab.Loss(fam, Cand(s), s) - OutcomeAccuracyLab.Loss(fam, Prod(s), s))).ToList());
                var gateProd = report.MarketEligibility.First(m => m.LeagueId == o && m.Family == fam).Status;
                var gateCand = CandidateGate(fam, o, ss, Cand, np, report.MarketEligibility.First(m => m.LeagueId == o && m.Family == fam).FinishedLast60Days);
                cells.Add(new { org = o, fam, n = a.N, coverage = a.Coverage, freqLL = f.LogLoss, prodLL = a.LogLoss, candLL = b.LogLoss, prodBrier = a.Brier, candBrier = b.Brier, prodEce = a.Ece, candEce = b.Ece, ci, gateProd, gateCand });
                sb.AppendLine($"| {o} | {fam} | {a.N} | {a.Coverage:0.00} | {f.LogLoss:0.0000} | {a.LogLoss:0.0000} | {b.LogLoss:0.0000} | {a.Brier:0.0000}/{b.Brier:0.0000} | {a.Ece:0.000}/{b.Ece:0.000} | {ci.Mean:+0.0000;-0.0000} [{ci.Low:+0.0000;-0.0000}, {ci.High:+0.0000;-0.0000}] | {gateProd} | {gateCand} |");
            }
        sb.AppendLine("\n## Ablasyon (bileşen kaldırılınca; + = kötüleşme, bileşen faydalı)");
        foreach (var kv in ablations) sb.AppendLine($"- {kv.Key}: {JsonSerializer.Serialize(kv.Value)}");
        sb.AppendLine("\n## Lig eksenleri (yalnız rapor; test daha önce görüldü)");
        foreach (var kv in axes) sb.AppendLine($"- {kv.Key}: {JsonSerializer.Serialize(kv.Value)}");
        sb.AppendLine($"\nsüre {sw.ElapsedMilliseconds} ms");

        var outDir = Environment.GetEnvironmentVariable("FORMAX_LAB_OUT");
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
            await File.WriteAllTextAsync(Path.Combine(outDir, $"accuracy-{tag}.md"), sb.ToString());
            await File.WriteAllTextAsync(Path.Combine(outDir, $"accuracy-{tag}.json"), JsonSerializer.Serialize(new
            {
                cutoff = Cutoff, evalStart, calStart, testStart, trainMatches = report.TrainMatches, valCount = val.Count, testCount = test.Count,
                deterministic, splitsDisjoint, datesOk, configs, bestC, eloKind, bestW, bestIc, bestBeta, candidateIsProd, parameters = chosen, overall, cells, ablations, axes,
                manifest = new { test = test.Select(s => s.MatchId).OrderBy(x => x), validation = val.Select(s => s.MatchId).OrderBy(x => x) }
            }, new JsonSerializerOptions { WriteIndented = false, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
        }
        Assert.True(deterministic);
        Assert.True(splitsDisjoint && datesOk);
    }

    /// <summary>Adayın hücre kapısı — mevcut politika (MarketEligibilityPolicy.Decide) aynen; eşik değişmez.</summary>
    private static string CandidateGate(string fam, int org, List<EvalSample> ss, Func<EvalSample, Pred> f, int notPredicted, int recent)
    {
        var m = new MarketFamilyMetrics { LeagueId = org, Family = fam, Matches = ss.Count, NotPredicted = notPredicted, FinishedLast60Days = recent };
        if (ss.Count == 0) { MarketEligibilityPolicy.Decide(m); return m.Status; }
        var diffs = ss.Select(s => OutcomeAccuracyLab.Loss(fam, f(s), s) - OutcomeAccuracyLab.Loss(fam, Pred.FromBaseline(s), s)).ToArray();
        m.LogLossDiff = diffs.Average();
        var familyIndex = MarketFamilies.Measured.ToList().IndexOf(fam);
        var (lo, hi) = GroupEvaluator.BootstrapMeanCi(diffs, EligibilityPolicy.BootstrapSamples, 5000 + org + familyIndex * 101);
        m.LogLossDiffCiLow = lo; m.LogLossDiffCiHigh = hi; m.SignificantlyBetter = hi < 0;
        m.CalibrationError = OutcomeAccuracyLab.Evaluate(fam, ss, f).Ece;
        if (fam == MarketFamilies.MatchResult)
        {
            var n = (double)ss.Count;
            var bh = ss.Sum(s => f(s).H - (s.HomeGoals > s.AwayGoals ? 1 : 0)) / n;
            var bd = ss.Sum(s => f(s).D - (s.HomeGoals == s.AwayGoals ? 1 : 0)) / n;
            var ba = ss.Sum(s => f(s).A - (s.HomeGoals < s.AwayGoals ? 1 : 0)) / n;
            m.MaxBias = new[] { bh, bd, ba }.OrderByDescending(Math.Abs).First();
        }
        else m.MaxBias = ss.Sum(s => f(s).Binary(fam) - (OutcomeAccuracyLab.Outcome(fam, s) ? 1 : 0)) / ss.Count;
        MarketEligibilityPolicy.Decide(m);
        return m.Status;
    }
}
