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
using Pick = Formax.Application.Services.Outcomes.SelectivePrediction.Pick;
using Row = Formax.Application.Services.Outcomes.SelectivePrediction.Row;

namespace Formax.Tests;

/// <summary>
/// SEÇİCİ TAHMİN — GERÇEK VERİ LABORATUVARI (26.09.2026). YALNIZ OKUR; dış istek yok. <c>FORMAX_LAB_SQL</c> yoksa atlanır.
/// Eşikler test ÖNCESİ örneklerden (eğitim + validation) öğrenilir; final test yalnız değerlendirilir. Model 4.0 değişmez.
/// </summary>
public class SelectivePredictionLabTests
{
    private static readonly DateTime Cutoff = OutcomeAccuracyLabTests.Cutoff;
    private static readonly int[] Orgs = { 2, 3, 39, 40, 61, 78, 88, 135, 140, 203, 848 };

    private static string Season(DateTime t) => t.Month >= 7 ? $"{t.Year}/{(t.Year + 1) % 100:00}" : $"{t.Year - 1}/{t.Year % 100:00}";

    [SkippableFact]
    public async Task Lab_SeciciTahmin_PrecisionCoverage_Esik_Model5Golge()
    {
        var conn = Environment.GetEnvironmentVariable("FORMAX_LAB_SQL");
        Skip.If(string.IsNullOrWhiteSpace(conn), "FORMAX_LAB_SQL yok");
        await using var db = new FormaxDbContext(new DbContextOptionsBuilder<FormaxDbContext>().UseSqlServer(conn, o => o.CommandTimeout(120)).Options);
        var loader = new OutcomeHistoryLoader(db);
        var history = await loader.LoadAsync(Cutoff);
        var names = await loader.LoadCompetitionNamesAsync();
        var testStart = Cutoff - OutcomeModelTrainingService.TestWindow;
        var calStart = testStart - OutcomeModelTrainingService.CalibrationWindow;
        var evalStart = calStart - OutcomeModelTrainingService.TrainWindow;
        var report = OutcomeBacktest.Run(history, CompetitionCatalog.Build(history, names), LockedCompetitions.All.ToHashSet(),
            evalStart, calStart, testStart, Cutoff, Cutoff, compareLegacy: false, candidate: true);
        var art = OutcomeBacktest.LastArtifacts!;
        var chosen = art.Chosen;
        var preTest = art.PreTestSamples!.Where(s => LockedCompetitions.All.Contains(s.LeagueId)).ToList();
        var test = art.LockedTestSamples.ToList();
        var configHash = BacktestEligibilityEvaluationSource.CurrentConfigHash;

        List<Pick> Picks(IEnumerable<EvalSample> ss, Func<EvalSample, ScoreDistribution> dist)
            => ss.SelectMany(s => { var d = dist(s); return SelectivePrediction.Markets.Select(m => SelectivePrediction.Choose(m, d, s)); }).ToList();
        ScoreDistribution M40(EvalSample s) => OutcomePredictor.Predict(s.E, s.LeagueId, chosen).Calibrated;
        var elo = new Dictionary<int, double>();
        DynamicElo.Replay(history, Cutoff, (m, x) => elo[m.MatchId] = x);
        ScoreDistribution M5(EvalSample s) => Model5Shadow.Predict(M40(s), s.LeagueId, s.E.CrossLeague, elo[s.MatchId]);

        var prePicks = Picks(preTest, M40);
        var testPicks = Picks(test, M40);
        var m5Picks = Picks(test, M5);

        // ── C) Eşikler: YALNIZ test öncesi ──
        var thresholds = SelectivePrediction.LearnThresholds(prePicks);
        // Final testte eşikleri öğrenmediğimizin kanıtı: eşik test örnekleri olmadan aynı çıkmalı.
        var thresholdsNoTest = SelectivePrediction.LearnThresholds(prePicks.Where(p => p.KickoffUtc < testStart).ToList());
        var learnedWithoutTest = JsonSerializer.Serialize(thresholds) == JsonSerializer.Serialize(thresholdsNoTest) && prePicks.All(p => p.KickoffUtc < testStart);

        var sb = new StringBuilder();
        var json = new Dictionary<string, object?>();
        sb.AppendLine("# Seçici tahmin laboratuvarı");
        sb.AppendLine($"model {OutcomeModelVersion.Current}; config {configHash}; eşik öğrenme = test öncesi [{evalStart:yyyy-MM-dd}, {testStart:yyyy-MM-dd}) {preTest.Count} maç; final test [{testStart:yyyy-MM-dd}, {Cutoff:yyyy-MM-dd}) {test.Count} maç");
        sb.AppendLine($"eşikler (test öncesi, Wilson alt ≥ {SelectivePrediction.TargetWilsonLow}, n ≥ {SelectivePrediction.MinLearnSamples}): {string.Join(", ", thresholds.Select(k => k.Key + "=" + (k.Value?.ToString("0.00") ?? "YOK")))}; test görülmeden öğrenildi={learnedWithoutTest}");
        json["thresholds"] = thresholds;

        void Table(string title, IReadOnlyList<Row> rows)
        {
            sb.AppendLine($"\n### {title}");
            sb.AppendLine("| Kapsam | N | Doğru | Yanlış | Doğruluk | %95 CI | Ort. tahmin | Kalibrasyon farkı | Min p |");
            sb.AppendLine("|---|---:|---:|---:|---:|---|---:|---:|---:|");
            foreach (var r in rows)
                sb.AppendLine($"| {r.Label} | {r.N} | {r.Correct} | {r.Wrong} | {r.Accuracy:P1} | [{r.CiLow:P1}, {r.CiHigh:P1}] | {r.MeanProbability:P1} | {r.CalibrationGap * 100:+0.0;-0.0} pp | {r.MinProbability:P1} |");
        }

        // ── A/B) Genel doğruluk + precision/coverage (final test) ──
        sb.AppendLine("\n## A) Model 4.0 genel doğruluk (final test)");
        sb.AppendLine("| Market | N | Doğruluk | %95 CI | Ort. tahmin | Fark |");
        sb.AppendLine("|---|---:|---:|---|---:|---:|");
        foreach (var m in SelectivePrediction.Markets)
        {
            var r = SelectivePrediction.Summarize(m, testPicks.Where(p => p.Market == m).ToList());
            sb.AppendLine($"| {m} | {r.N} | {r.Accuracy:P1} | [{r.CiLow:P1}, {r.CiHigh:P1}] | {r.MeanProbability:P1} | {r.CalibrationGap * 100:+0.0;-0.0} pp |");
        }
        json["pc"] = SelectivePrediction.Markets.ToDictionary(m => m, m => SelectivePrediction.PrecisionCoverage(testPicks.Where(p => p.Market == m).ToList()));
        foreach (var m in SelectivePrediction.Markets) Table($"B) Precision/coverage — {m}", SelectivePrediction.PrecisionCoverage(testPicks.Where(p => p.Market == m).ToList()));
        Table("B) Precision/coverage — bütün marketler birlikte (kolay marketler dâhil)", SelectivePrediction.PrecisionCoverage(testPicks));
        Table("B) Precision/coverage — yalnız gol marketleri (1.5/2.5/3.5)", SelectivePrediction.PrecisionCoverage(testPicks.Where(p => p.Market is MarketFamilies.TotalGoals15 or MarketFamilies.TotalGoals25 or MarketFamilies.TotalGoals35).ToList()));
        var r1x2 = testPicks.Where(p => p.Market == MarketFamilies.MatchResult).ToList();
        foreach (var o in new[] { "1", "X", "2" }) Table($"B) 1X2 — seçilen sonuç {o}", SelectivePrediction.PrecisionCoverage(r1x2.Where(p => p.Outcome == o).ToList()));
        foreach (var season in r1x2.Select(p => Season(p.KickoffUtc)).Distinct().OrderBy(x => x))
            Table($"B) 1X2 — sezon {season}", SelectivePrediction.PrecisionCoverage(r1x2.Where(p => Season(p.KickoffUtc) == season).ToList()));
        sb.AppendLine("\n### B) Organizasyon — 1X2 (en güçlü %10 / tümü) ve ÇŞ en güçlü %10");
        sb.AppendLine("| Org | 1X2 N | 1X2 tümü | 1X2 en güçlü %10 [CI] | ÇŞ en güçlü %10 [CI] | 1.5 en güçlü %10 [CI] |");
        sb.AppendLine("|---|---:|---:|---|---|---|");
        foreach (var o in Orgs)
        {
            Row Top(string m) => SelectivePrediction.PrecisionCoverage(testPicks.Where(p => p.LeagueId == o && p.Market == m).ToList())[3];
            var all = SelectivePrediction.Summarize("", r1x2.Where(p => p.LeagueId == o).ToList());
            var a = Top(MarketFamilies.MatchResult); var d = Top(MarketFamilies.DoubleChance); var g = Top(MarketFamilies.TotalGoals15);
            sb.AppendLine($"| {o} | {all.N} | {all.Accuracy:P1} | {a.Accuracy:P1} (n={a.N}) [{a.CiLow:P0}, {a.CiHigh:P0}] | {d.Accuracy:P1} (n={d.N}) [{d.CiLow:P0}, {d.CiHigh:P0}] | {g.Accuracy:P1} (n={g.N}) [{g.CiLow:P0}, {g.CiHigh:P0}] |");
        }

        // ── E) Kalibrasyon bantları (test öncesi ve final test) ──
        foreach (var m in SelectivePrediction.Markets)
        {
            Table($"E) Kalibrasyon bantları — {m} — final test", SelectivePrediction.CalibrationBands(testPicks.Where(p => p.Market == m).ToList()));
        }
        json["bandsPre"] = SelectivePrediction.Markets.ToDictionary(m => m, m => SelectivePrediction.CalibrationBands(prePicks.Where(p => p.Market == m).ToList()));
        json["bandsTest"] = SelectivePrediction.Markets.ToDictionary(m => m, m => SelectivePrediction.CalibrationBands(testPicks.Where(p => p.Market == m).ToList()));

        // ── C/H) Güçlü katman — final test değerlendirmesi ──
        List<(string Tier, Pick? Strong, int MatchId)> Tiering(List<EvalSample> ss, List<Pick> picks)
        {
            var byMatch = picks.GroupBy(p => p.MatchId).ToDictionary(g => g.Key, g => g.ToList());
            return ss.Select(s => { var (t, p) = SelectivePrediction.Tier(byMatch[s.MatchId], thresholds, s.E.Sufficient); return (t, p, s.MatchId); }).ToList();
        }
        var tiers = Tiering(test, testPicks);
        var strong = tiers.Where(t => t.Strong != null).Select(t => t.Strong!).ToList();
        var sRow = SelectivePrediction.Summarize("Strongest (final test)", strong);
        var byMarket = strong.GroupBy(p => p.Market).ToDictionary(g => g.Key, g => SelectivePrediction.Summarize(g.Key, g.ToList()));
        var byOrg = strong.GroupBy(p => p.LeagueId).ToDictionary(g => g.Key, g => SelectivePrediction.Summarize(g.Key.ToString(), g.ToList()));
        var bySeason = strong.GroupBy(p => Season(p.KickoffUtc)).ToDictionary(g => g.Key, g => SelectivePrediction.Summarize(g.Key, g.ToList()));
        var easy = strong.Count(p => p.Market is MarketFamilies.DoubleChance or MarketFamilies.TotalGoals15);
        var dcShare = strong.Count == 0 ? 0 : (double)strong.Count(p => p.Market == MarketFamilies.DoubleChance) / strong.Count;
        var maxOrgShare = strong.Count == 0 ? 0 : byOrg.Values.Max(r => r.N) / (double)strong.Count;
        var maxMarketShare = strong.Count == 0 ? 0 : byMarket.Values.Max(r => r.N) / (double)strong.Count;
        var periodsOk = bySeason.Count(kv => kv.Value.N >= 30) >= 3;
        var brier = strong.Count == 0 ? 0 : strong.Average(p => Math.Pow(p.Probability - (p.Correct == true ? 1 : 0), 2));
        var ll = strong.Count == 0 ? 0 : strong.Average(p => -Math.Log(Math.Max(1e-6, p.Correct == true ? p.Probability : 1 - p.Probability)));
        var gates = new Dictionary<string, bool>
        {
            ["n>=200"] = sRow.N >= 200,
            ["periods>=3 (her biri ≥30)"] = periodsOk,
            ["accuracy>=0.85"] = sRow.Accuracy >= 0.85,
            ["wilsonLow>=0.80"] = sRow.CiLow >= 0.80,
            ["|meanP-acc|<=0.05"] = Math.Abs(sRow.CalibrationGap) <= 0.05,
            ["tek org payı <= %50"] = maxOrgShare <= 0.5,
            ["tek market payı <= %50"] = maxMarketShare <= 0.5
        };
        sb.AppendLine("\n## C/H) FORMAX En Güçlü — final test (eşikler test öncesinden)");
        sb.AppendLine($"katman dağılımı: {string.Join(", ", tiers.GroupBy(t => t.Tier).Select(g => $"{g.Key}={g.Count()}"))}; kapsam {(double)strong.Count / Math.Max(1, test.Count):P1} maç");
        sb.AppendLine($"Strongest: n={sRow.N} doğru={sRow.Correct} doğruluk={sRow.Accuracy:P1} CI [{sRow.CiLow:P1}, {sRow.CiHigh:P1}] ort.p={sRow.MeanProbability:P1} fark={sRow.CalibrationGap * 100:+0.0;-0.0}pp Brier={brier:0.0000} LL={ll:0.0000}");
        sb.AppendLine($"market payları: {string.Join(", ", byMarket.Select(kv => $"{kv.Key}={kv.Value.N} ({kv.Value.Accuracy:P1})"))}; kolay (ÇŞ+1.5) pay={(double)easy / Math.Max(1, strong.Count):P1}; ÇŞ payı={dcShare:P1}");
        sb.AppendLine($"org payları: {string.Join(", ", byOrg.OrderByDescending(kv => kv.Value.N).Select(kv => $"{kv.Key}={kv.Value.N} ({kv.Value.Accuracy:P1})"))}");
        sb.AppendLine($"sezon: {string.Join(", ", bySeason.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value.N} ({kv.Value.Accuracy:P1} [{kv.Value.CiLow:P0},{kv.Value.CiHigh:P0}])"))}");
        sb.AppendLine($"kapılar: {string.Join(", ", gates.Select(kv => kv.Key + "=" + (kv.Value ? "GEÇTİ" : "KALDI")))}");
        // Kolay marketler hariç (1X2 + 2.5 + 3.5) güçlü katman
        var hardThr = thresholds.ToDictionary(k => k.Key, k => k.Key is MarketFamilies.DoubleChance or MarketFamilies.TotalGoals15 ? null : k.Value);
        var hard = test.Select(s => SelectivePrediction.Tier(testPicks.Where(p => p.MatchId == s.MatchId).ToList(), hardThr, s.E.Sufficient).Strongest).Where(p => p != null).Select(p => p!).ToList();
        var hRow = SelectivePrediction.Summarize("Strongest (ÇŞ ve 1.5 hariç)", hard);
        sb.AppendLine($"ÇŞ ve 1.5 hariç güçlü katman: n={hRow.N} doğruluk={hRow.Accuracy:P1} CI [{hRow.CiLow:P1}, {hRow.CiHigh:P1}] marketler: {string.Join(", ", hard.GroupBy(p => p.Market).Select(g => g.Key + "=" + g.Count()))}");
        json["strongest"] = new { sRow, byMarket, byOrg, bySeason, gates, dcShare, easyShare = (double)easy / Math.Max(1, strong.Count), brier, ll, hard = hRow, coverage = (double)strong.Count / Math.Max(1, test.Count) };

        // ── C') KEŞİF: final kapıyla uyumlu B kuralı (test öncesi doğruluk ≥ %90, Wilson alt ≥ %80, n ≥ 50) — test daha önce
        //     görüldüğü için ÜRETİM kararı değildir; ileriye dönük gölge için önceden kaydedilir.
        var thrB = SelectivePrediction.LearnThresholdsForward(prePicks);
        var strongB = test.Select(s => SelectivePrediction.Tier(testPicks.Where(p => p.MatchId == s.MatchId).ToList(), thrB, true).Strongest).Where(p => p != null).Select(p => p!).ToList();
        var bRow = SelectivePrediction.Summarize("B kuralı", strongB);
        var bSeason = strongB.GroupBy(p => Season(p.KickoffUtc)).OrderBy(g => g.Key).Select(g => { var r = SelectivePrediction.Summarize(g.Key, g.ToList()); return $"{g.Key}={r.N} ({r.Accuracy:P1} [{r.CiLow:P0},{r.CiHigh:P0}])"; });
        var bOrg = strongB.GroupBy(p => p.LeagueId).OrderByDescending(g => g.Count()).Select(g => { var r = SelectivePrediction.Summarize("", g.ToList()); return $"{g.Key}={r.N} ({r.Accuracy:P1})"; });
        sb.AppendLine("\n## C') KEŞİF — B kuralı (test görüldü; üretim kararı değil, gölge için önceden kayıt)");
        sb.AppendLine($"eşikler: {string.Join(", ", thrB.Select(k => k.Key + "=" + (k.Value?.ToString("0.00") ?? "YOK")))}");
        sb.AppendLine($"n={bRow.N} doğruluk={bRow.Accuracy:P1} CI [{bRow.CiLow:P1}, {bRow.CiHigh:P1}] ort.p={bRow.MeanProbability:P1} fark={bRow.CalibrationGap * 100:+0.0;-0.0}pp kapsam={(double)strongB.Count / test.Count:P1}");
        sb.AppendLine($"marketler: {string.Join(", ", strongB.GroupBy(p => p.Market).Select(g => { var r = SelectivePrediction.Summarize("", g.ToList()); return $"{g.Key}={r.N} ({r.Accuracy:P1})"; }))}");
        sb.AppendLine($"sezon: {string.Join(", ", bSeason)}; org: {string.Join(", ", bOrg)}");
        json["ruleB"] = new { thrB, bRow };

        // ── E') Platt (ikili) / sıcaklık (1X2) kalibrasyon adayı — YALNIZ validation'da uydurulur ──
        var valS = art.CalibrationSamples.Where(s => LockedCompetitions.All.Contains(s.LeagueId)).ToList();
        sb.AppendLine("\n## E') Yeniden kalibrasyon adayı (validation'da uyduruldu, final testte ölçüldü)");
        foreach (var fam in new[] { MarketFamilies.TotalGoals15, MarketFamilies.TotalGoals25, MarketFamilies.TotalGoals35 })
        {
            double Q(ScoreDistribution d) => fam == MarketFamilies.TotalGoals15 ? d.Over(1.5) : fam == MarketFamilies.TotalGoals25 ? d.Over(2.5) : d.Over(3.5);
            bool Y(EvalSample s) => OutcomeAccuracyLab.Outcome(fam, s);
            var (slope, intercept) = MarketFamilyEvaluator.CalibrationFit(valS.Select(s => (Q(M40(s)), Y(s))).ToList());
            double Platt(double p) { var pc = Math.Clamp(p, 1e-6, 1 - 1e-6); return 1 / (1 + Math.Exp(-(intercept!.Value + slope!.Value * Math.Log(pc / (1 - pc))))); }
            double LL(Func<double, double> f) => test.Average(s => { var q = Math.Clamp(f(Q(M40(s))), 1e-6, 1 - 1e-6); return Y(s) ? -Math.Log(q) : -Math.Log(1 - q); });
            double Br(Func<double, double> f) => test.Average(s => Math.Pow(f(Q(M40(s))) - (Y(s) ? 1 : 0), 2));
            double Ece(Func<double, double> f) => OutcomeBacktest.Ece(test.SelectMany(s => { var q = f(Q(M40(s))); return new[] { (q, Y(s)), (1 - q, !Y(s)) }; }).ToList(), 10);
            sb.AppendLine($"- {fam}: Platt a={intercept:0.000} b={slope:0.000} | LL {LL(p => p):0.00000}→{LL(Platt):0.00000} | Brier {Br(p => p):0.00000}→{Br(Platt):0.00000} | ECE {Ece(p => p):0.0000}→{Ece(Platt):0.0000}");
        }
        {
            // 1X2 sıcaklık ölçekleme: p_i ∝ p_i^(1/T); T validation'da ızgarada seçilir.
            ScoreDistribution D(EvalSample s) => M40(s);
            (double H, double X, double A) Temp((double H, double X, double A) p, double t) { double h = Math.Pow(p.H, 1 / t), x = Math.Pow(p.X, 1 / t), a = Math.Pow(p.A, 1 / t); var z = h + x + a; return (h / z, x / z, a / z); }
            double L((double H, double X, double A) p, EvalSample s) => -Math.Log(Math.Max(1e-6, s.HomeGoals > s.AwayGoals ? p.H : s.HomeGoals == s.AwayGoals ? p.X : p.A));
            var grid = new[] { 0.9, 0.95, 1.0, 1.05, 1.1, 1.2 };
            var bestT = grid.OrderBy(t => valS.Average(s => { var d = D(s); return L(Temp((d.HomeWin, d.Draw, d.AwayWin), t), s); })).First();
            var before = test.Average(s => { var d = D(s); return L((d.HomeWin, d.Draw, d.AwayWin), s); });
            var after = test.Average(s => { var d = D(s); return L(Temp((d.HomeWin, d.Draw, d.AwayWin), bestT), s); });
            sb.AppendLine($"- 1X2 sıcaklık: validation T={bestT} | test LL {before:0.00000}→{after:0.00000}");
        }

        // ── F) Model 5 gölge — final testte 4.0 ile karşılaştırma (yalnız bilgi; parametreler ÖNCEDEN kayıtlı) ──
        double LL1X2(Func<EvalSample, ScoreDistribution> f, IEnumerable<EvalSample> ss) => ss.Average(s => { var d = f(s); return -Math.Log(Math.Max(1e-6, s.HomeGoals > s.AwayGoals ? d.HomeWin : s.HomeGoals == s.AwayGoals ? d.Draw : d.AwayWin)); });
        var domestic = test.Where(s => Model5Shadow.Applies(s.LeagueId, s.E.CrossLeague)).ToList();
        var diffs = test.Select(s =>
        {
            double L(ScoreDistribution d) => -Math.Log(Math.Max(1e-6, s.HomeGoals > s.AwayGoals ? d.HomeWin : s.HomeGoals == s.AwayGoals ? d.Draw : d.AwayWin));
            return (s.KickoffUtc, L(M5(s)) - L(M40(s)));
        }).ToList();
        var ci = OutcomeAccuracyLab.PairedBlockBootstrap(diffs);
        sb.AppendLine($"\n## F) Model 5 gölge ({Model5Shadow.Version}, config {Model5Shadow.ConfigHash}) — bilgi amaçlı, üretime ALINMAZ");
        sb.AppendLine($"1X2 LL 4.0={LL1X2(M40, test):0.00000} M5={LL1X2(M5, test):0.00000} Δ={ci.Mean:+0.00000;-0.00000} [{ci.Low:+0.00000;-0.00000}, {ci.High:+0.00000;-0.00000}]; uygulanan maç {domestic.Count}/{test.Count}. NOT: bu test penceresi M5 keşfinden önce görüldü; kanıt yalnız ileriye dönük gölge kayıttan gelecek.");

        var outDir = Environment.GetEnvironmentVariable("FORMAX_LAB_OUT");
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
            await File.WriteAllTextAsync(Path.Combine(outDir, "selective.md"), sb.ToString());
            await File.WriteAllTextAsync(Path.Combine(outDir, "selective.json"), JsonSerializer.Serialize(json));
        }
        Assert.True(learnedWithoutTest);
    }
}
