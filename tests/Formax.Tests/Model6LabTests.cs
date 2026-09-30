using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace Formax.Tests;

/// <summary>
/// MODEL 6 — 1X2 NESTED WALK-FORWARD LABORATUVARI (30.09.2026). YALNIZ OKUR; dış istek yok. <c>FORMAX_LAB_SQL</c> yoksa atlanır.
///
/// Düzen: Model 4.0, 26.09 kesimiyle aynen dondurulur (parametreleri [2024-04-08, 2025-02-02) verisiyle seçilmiştir). Final test
/// penceresi [2025-02-02, 2026-09-25) eşit maç sayılı 6 DIŞ fold'a bölünür. Her dış fold'da bütün aday seçimi (konfigürasyon, aile,
/// ensemble ağırlığı, kalibrasyon) YALNIZ o fold'dan önceki kilitli maçlarla yapılır (genişleyen iç pencere [2024-09-05, fold başı)).
/// Aday motorlar tek geçişte her maç için yalnız maçtan önce bilinen sonuçlarla tahmin üretir; seçim yalnız pencere dilimler.
/// Final test daha önce görüldüğü için bu kanıt yalnız geriye dönüktür; üretim kararı ileriye dönük gölgeden gelir.
/// </summary>
public class Model6LabTests
{
    private static readonly DateTime Cutoff = OutcomeAccuracyLabTests.Cutoff;
    private static readonly int[] Orgs = { 2, 3, 39, 40, 61, 78, 88, 135, 140, 203, 848 };
    private static readonly HashSet<int> Uefa = new() { 2, 3, 848 };
    private const int OuterFoldCount = 6;
    private const double MinGain = OutcomeBacktest.MinimumCalibrationGain;
    private const int SegmentMinSample = 300;

    private sealed record S(int MatchId, int LeagueId, DateTime KickoffUtc, int HomeTeamId, int AwayTeamId, int Y, bool Cross);

    [SkippableFact]
    public async Task Lab_Model6_NestedWalkForward_1X2()
    {
        var conn = Environment.GetEnvironmentVariable("FORMAX_LAB_SQL");
        Skip.If(string.IsNullOrWhiteSpace(conn), "FORMAX_LAB_SQL yok");
        var outDir = Environment.GetEnvironmentVariable("FORMAX_LAB_OUT") ?? Path.GetTempPath();
        Directory.CreateDirectory(outDir);
        await using var db = new FormaxDbContext(new DbContextOptionsBuilder<FormaxDbContext>().UseSqlServer(conn, o => o.CommandTimeout(120)).Options);
        var loader = new OutcomeHistoryLoader(db);
        var history = await loader.LoadAsync(Cutoff);
        var names = await loader.LoadCompetitionNamesAsync();
        var catalog = CompetitionCatalog.Build(history, names);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sb = new StringBuilder();
        var json = new Dictionary<string, object?>();
        var inv = CultureInfo.InvariantCulture;

        // ════════════════ A) MODEL 4.0 DONDURULMUŞ BASELINE ════════════════
        var testStart = Cutoff - OutcomeModelTrainingService.TestWindow;
        var calStart = testStart - OutcomeModelTrainingService.CalibrationWindow;
        var evalStart = calStart - OutcomeModelTrainingService.TrainWindow;
        var report = OutcomeBacktest.Run(history, catalog, LockedCompetitions.All.ToHashSet(), evalStart, calStart, testStart, Cutoff, Cutoff,
            compareLegacy: false, candidate: true);
        var art = OutcomeBacktest.LastArtifacts!;
        var chosen = art.Chosen; var baseP = art.BaseParams;
        var configHash = BacktestEligibilityEvaluationSource.CurrentConfigHash;
        var byId = history.ToDictionary(m => m.MatchId);
        var test = art.LockedTestSamples.OrderBy(s => s.KickoffUtc).ThenBy(s => s.MatchId).ToList();
        var inner0 = art.PreTestSamples!.Where(s => s.KickoffUtc >= calStart && LockedCompetitions.All.Contains(s.LeagueId)).ToList();
        var evalSamples = inner0.Concat(test).OrderBy(s => s.KickoffUtc).ThenBy(s => s.MatchId).ToList();
        var S0 = evalSamples.ToDictionary(s => s.MatchId, s => new S(s.MatchId, s.LeagueId, s.KickoffUtc, byId[s.MatchId].HomeTeamId, byId[s.MatchId].AwayTeamId,
            Model6Lab.Outcome(s.HomeGoals, s.AwayGoals), s.E.CrossLeague));
        var m4 = evalSamples.ToDictionary(s => s.MatchId, s => Probs3.From(OutcomePredictor.Predict(s.E, s.LeagueId, chosen).Calibrated));
        var m4Raw = evalSamples.ToDictionary(s => s.MatchId, s => Probs3.From(OutcomePredictor.Predict(s.E, s.LeagueId, baseP).Raw));
        var freq = evalSamples.ToDictionary(s => s.MatchId, s => Probs3.Normalized(s.BaseHome, s.BaseDraw, s.BaseAway));
        var manifest = test.Select(s => s.MatchId).ToList();
        var testSet = manifest.ToHashSet();

        // Model 4 tekrar üretilebilir mi? Aynı koşu ikinci kez → aynı olasılıklar.
        var again = OutcomeBacktest.Run(history, catalog, LockedCompetitions.All.ToHashSet(), evalStart, calStart, testStart, Cutoff, Cutoff, compareLegacy: false, candidate: true);
        var art2 = OutcomeBacktest.LastArtifacts!;
        var m4Deterministic = art2.LockedTestSamples.Count == test.Count && art2.LockedTestSamples.All(s =>
            m4.TryGetValue(s.MatchId, out var p) && Math.Abs(Probs3.From(OutcomePredictor.Predict(s.E, s.LeagueId, art2.Chosen).Calibrated).H - p.H) < 1e-12);

        var folds = Model6Lab.OuterFolds(test.Select(s => s.KickoffUtc).ToList(), testStart, Cutoff, OuterFoldCount);
        int FoldOf(DateTime t) => folds.FindIndex(f => t >= f.From && t < f.To);

        {
            var csv = new StringBuilder("MatchId,LeagueId,KickoffUtc,HomeTeamId,AwayTeamId,HomeGoals,AwayGoals,Actual,HomeProbability,DrawProbability,AwayProbability,Argmax,Correct,LogLoss,Brier,ModelVersion,ConfigHash,DataCutUtc,OuterFold\n");
            foreach (var s in test)
            {
                var p = m4[s.MatchId]; var y = S0[s.MatchId].Y; var k = p.Argmax;
                csv.AppendLine(string.Join(",", s.MatchId, s.LeagueId, s.KickoffUtc.ToString("O"), S0[s.MatchId].HomeTeamId, S0[s.MatchId].AwayTeamId, s.HomeGoals, s.AwayGoals,
                    "HDA"[y], p.H.ToString("0.000000", inv), p.D.ToString("0.000000", inv), p.A.ToString("0.000000", inv), "HDA"[k], k == y ? 1 : 0,
                    Model6Lab.LogLoss(p, y).ToString("0.000000", inv), Model6Lab.Brier(p, y).ToString("0.000000", inv), OutcomeModelVersion.Current, configHash,
                    // Veri kesimi: bu maçın tahmini yalnız başlama saati bu maçtan ÖNCE olan sonuçları görür (kickoff-batch).
                    s.KickoffUtc.ToString("O"), FoldOf(s.KickoffUtc) + 1));
            }
            await File.WriteAllTextAsync(Path.Combine(outDir, "model4-baseline.csv"), csv.ToString());
        }

        List<(Probs3 P, int Y)> Pairs(IEnumerable<int> ids, IReadOnlyDictionary<int, Probs3> model) => ids.Select(id => (model[id], S0[id].Y)).ToList();
        var m4Test = Model6Lab.Evaluate(Pairs(manifest, m4));
        sb.AppendLine("# Model 6 — 1X2 nested walk-forward laboratuvarı");
        sb.AppendLine($"kesim {Cutoff:O}; Model 4 config {configHash}; parametre seçim verisi [{evalStart:yyyy-MM-dd}, {testStart:yyyy-MM-dd}); manifest {test.Count} maç (kapsam dışı {report.InsufficientDataMatches}); Model 4 determinizm={m4Deterministic}");
        sb.AppendLine("\n## A) Model 4.0 baseline (final test manifesti)");
        sb.AppendLine(MetricsLine("Model 4.0", m4Test));
        sb.AppendLine($"karışıklık [gerçek→tahmin E/B/D]: {JsonSerializer.Serialize(m4Test.Confusion)}; precision {Join(m4Test.Precision)}; recall {Join(m4Test.Recall)}");
        sb.AppendLine($"kapsam: tahmin {test.Count} / {test.Count + report.InsufficientDataMatches} = {(double)test.Count / (test.Count + report.InsufficientDataMatches):P1}");
        sb.AppendLine("\n| Org | N | Acc | LL | Brier | ECE |\n|---|---|---|---|---|---|");
        foreach (var o in Orgs)
        {
            var mm = Model6Lab.Evaluate(Pairs(manifest.Where(id => S0[id].LeagueId == o), m4));
            sb.AppendLine($"| {o} | {mm.N} | {mm.Accuracy:P1} | {mm.LogLoss:0.00000} | {mm.Brier:0.00000} | {mm.Ece:0.00000} |");
        }
        sb.AppendLine("\nSınıf kalibrasyon eğrileri (kova: n, ort. tahmin → gerçekleşme):");
        for (var c = 0; c < 3; c++)
            sb.AppendLine($"- {Model6Lab.ClassNames[c]}: " + string.Join("; ", Model6Lab.ClassCurve(Pairs(manifest, m4), c).Select(b => $"[{b.Lo:0.0}-{b.Hi:0.0}) n={b.N} {b.MeanP:P1}→{b.Rate:P1}")));
        sb.AppendLine("\nBirinci − ikinci olasılık farkına göre doğruluk:");
        foreach (var (lo, hi) in new[] { (0.0, 0.05), (0.05, 0.10), (0.10, 0.20), (0.20, 0.30), (0.30, 0.50), (0.50, 1.01) })
        {
            var band = manifest.Where(id => { var p = m4[id]; var o = new[] { p.H, p.D, p.A }.OrderByDescending(v => v).ToArray(); var g = o[0] - o[1]; return g >= lo && g < hi; }).ToList();
            if (band.Count == 0) continue;
            sb.AppendLine($"- fark [{lo:0.00}, {Math.Min(hi, 1):0.00}): n={band.Count} doğruluk {band.Average(id => m4[id].Argmax == S0[id].Y ? 1.0 : 0.0):P1}");
        }
        json["model4"] = new { configHash, manifestCount = test.Count, notPredicted = report.InsufficientDataMatches, metrics = m4Test, deterministic = m4Deterministic };

        // ════════════════ B) BERABERLİK TEŞHİSİ ════════════════
        {
            var P = Pairs(manifest, m4);
            var draws = P.Count(x => x.Y == 1);
            var drawTop = P.Count(x => x.P.Argmax == 1);
            var second = P.Count(x => { var o = new[] { (0, x.P.H), (1, x.P.D), (2, x.P.A) }.OrderByDescending(v => v.Item2).ToArray(); return o[1].Item1 == 1; });
            var gaps = P.Select(x => Math.Max(x.P.H, x.P.A) - x.P.D).OrderBy(v => v).ToList();
            double Q(double q) => gaps[(int)(q * (gaps.Count - 1))];
            var balanced = P.Where(x => Math.Abs(x.P.H - x.P.A) < 0.10).ToList();
            var maxDraw = P.Max(x => x.P.D);
            sb.AppendLine("\n## B) Beraberlik teşhisi (Model 4.0, manifest)");
            sb.AppendLine($"gerçek beraberlik {draws} / {P.Count} = {(double)draws / P.Count:P2}; ort. DrawP {P.Average(x => x.P.D):P2}; gerçek beraberlikte {P.Where(x => x.Y == 1).Average(x => x.P.D):P2}; beraberlik olmayanlarda {P.Where(x => x.Y != 1).Average(x => x.P.D):P2}; en yüksek DrawP {maxDraw:P2}");
            sb.AppendLine($"argmax=beraberlik {drawTop} kez; beraberlik ikinci sırada {second} maç; (maks(E,D) − B) farkı: min {gaps[0]:0.000} p10 {Q(0.1):0.000} medyan {Q(0.5):0.000} p90 {Q(0.9):0.000}");
            sb.AppendLine($"ev: ort. tahmin {P.Average(x => x.P.H):P2} / gerçek {P.Average(x => x.Y == 0 ? 1.0 : 0):P2}; deplasman: {P.Average(x => x.P.A):P2} / {P.Average(x => x.Y == 2 ? 1.0 : 0):P2}");
            sb.AppendLine($"dengeli maç (|E−D|<0,10) n={balanced.Count}: gerçek beraberlik {balanced.Average(x => x.Y == 1 ? 1.0 : 0):P1}, ort. DrawP {balanced.Average(x => x.P.D):P1}, ev {balanced.Average(x => x.Y == 0 ? 1.0 : 0):P1} (tahmin {balanced.Average(x => x.P.H):P1}), dep {balanced.Average(x => x.Y == 2 ? 1.0 : 0):P1} (tahmin {balanced.Average(x => x.P.A):P1})");
            sb.AppendLine("DrawProbability bantları: " + string.Join("; ", Model6Lab.ClassCurve(P, 1, 0.02).Select(b => $"[{b.Lo:0.00}-{b.Hi:0.00}) n={b.N} {b.MeanP:P1}→{b.Rate:P1}")));
            sb.AppendLine("\n| Org | N | Gerçek B | Ort. DrawP | Ort. EvP / gerçek | Ort. DepP / gerçek |\n|---|---|---|---|---|---|");
            foreach (var o in Orgs)
            {
                var q = P.Where((_, i) => S0[manifest[i]].LeagueId == o).ToList();
                if (q.Count == 0) continue;
                sb.AppendLine($"| {o} | {q.Count} | {q.Average(x => x.Y == 1 ? 1.0 : 0):P1} | {q.Average(x => x.P.D):P1} | {q.Average(x => x.P.H):P1} / {q.Average(x => x.Y == 0 ? 1.0 : 0):P1} | {q.Average(x => x.P.A):P1} / {q.Average(x => x.Y == 2 ? 1.0 : 0):P1} |");
            }
            // Beraberliği her maçta tahmin et / hiç etme — argmax'ın doğruluğa etkisi (teşhis)
            var alwaysDrawBetter = balanced.Count(x => x.Y == 1) > balanced.Count(x => x.Y == x.P.Argmax);
            sb.AppendLine($"dengeli maçlarda 'beraberlik' demek argmax'tan daha çok tutar mıydı? {alwaysDrawBetter} (beraberlik {balanced.Count(x => x.Y == 1)} vs argmax {balanced.Count(x => x.Y == x.P.Argmax)})");
            // Yön kontrolü: bütün organizasyonlarda iç saha üstünlüğü pozitif mi?
            var dir = history.Where(m => m.KickoffUtc >= calStart && LockedCompetitions.All.Contains(m.LeagueId)).GroupBy(m => m.LeagueId)
                .Select(g => (g.Key, Home: g.Average(m => (double)m.HomeGoals), Away: g.Average(m => (double)m.AwayGoals), HW: g.Average(m => m.HomeGoals > m.AwayGoals ? 1.0 : 0), AW: g.Average(m => m.HomeGoals < m.AwayGoals ? 1.0 : 0))).ToList();
            sb.AppendLine("yön kontrolü (ev gol/dep gol, ev G/dep G): " + string.Join("; ", dir.OrderBy(d => d.Key).Select(d => $"{d.Key}: {d.Home:0.00}/{d.Away:0.00}, {d.HW:P0}/{d.AW:P0}")));
            json["draw"] = new { draws, n = P.Count, drawTop, second, meanDrawP = P.Average(x => x.P.D), homeDirOk = dir.All(d => d.Home > d.Away && d.HW > d.AW) };
        }

        // ════════════════ C) SIZINTI / ZAMAN KONTROLÜ ════════════════
        {
            // Model 4 backtest'i yalnız TAM aynı saatte başlayan maçları birlikte işler. Başlama farkı < 120 dk olan (henüz bitmemiş)
            // aynı lig maçının sonucu lig ortalamasına girebilir — bu maçları say ve Model 4'ü onlarsız da ölç.
            var byLeague = history.GroupBy(m => m.LeagueId).ToDictionary(g => g.Key, g => g.Select(m => m.KickoffUtc).OrderBy(x => x).ToArray());
            var exposed = manifest.Where(id =>
            {
                var s = S0[id]; var arr = byLeague[s.LeagueId];
                return arr.Any(t => t < s.KickoffUtc && t > s.KickoffUtc.AddMinutes(-120));
            }).ToHashSet();
            var clean = Model6Lab.Evaluate(Pairs(manifest.Where(id => !exposed.Contains(id)), m4));
            sb.AppendLine("\n## C) Zaman kuralı");
            sb.AppendLine($"Model 4: aynı ligde 120 dk içinde önce başlamış (bitmemiş olabilecek) maçı olan manifest maçı {exposed.Count}; bunlar hariç Model 4 LL {clean.LogLoss:0.00000} (tümü {m4Test.LogLoss:0.00000})");
            sb.AppendLine("Aday motorlar: sonuç ancak başlama + 120 dk'dan SONRA başlayan maçlara girer (DavidsonElo.AdvanceTo). Bölmeler aşağıda doğrulanır.");
            json["leak"] = new { m4ExposedTo120mOverlap = exposed.Count, m4LLWithoutExposed = clean.LogLoss };
        }

        // ════════════════ E) TABAN MODELLER ════════════════
        var eloExisting = new Dictionary<int, double>();
        DynamicElo.Replay(history, Cutoff, (m, x) => { if (S0.ContainsKey(m.MatchId)) eloExisting[m.MatchId] = x; });
        var internalElo = evalSamples.ToDictionary(s => s.MatchId, s => { var q = Math.Clamp(s.E.EloHomeExpectation, 1e-4, 1 - 1e-4); return Math.Log(q / (1 - q)); });
        Probs3 Ordered(double x, double c) { var p = OutcomeAccuracyLab.EloPred(x, c); return Probs3.Normalized(p.H, p.D, p.A); }
        var eloC = new[] { 0.35, 0.50, 0.65 };
        var eloExistingBy = eloC.ToDictionary(c => c, c => (IReadOnlyDictionary<int, Probs3>)eloExisting.ToDictionary(k => k.Key, k => Ordered(k.Value, c)));
        var eloInternalBy = eloC.ToDictionary(c => c, c => (IReadOnlyDictionary<int, Probs3>)internalElo.ToDictionary(k => k.Key, k => Ordered(k.Value, c)));

        // ════════════════ F) ADAY AİLELERİ — tek geçiş, her maç yalnız geçmişle ════════════════
        var configs = new List<(string Family, string Name, string Hash)>();
        var davidson = new Dictionary<string, IReadOnlyDictionary<int, Probs3>>();
        var davidsonCfg = new Dictionary<string, DavidsonEloConfig>();
        IReadOnlyDictionary<int, Probs3> RunDavidson(DavidsonEloConfig c)
        {
            var d = new Dictionary<int, Probs3>();
            DavidsonElo.Replay(history, catalog, c, Cutoff, (m, p) => { if (S0.ContainsKey(m.MatchId)) d[m.MatchId] = p; });
            return d;
        }
        string DName(DavidsonEloConfig c) => string.Create(inv, $"K={c.K};carry={c.SeasonCarry};promo={c.PromotionPrior};form={c.FormWeight};rest={c.RestBeta}")
            + (c.FormRecency ? "" : ";flat") + (c.LeagueDraw ? "" : ";globalNu") + (c.HomeAdvantage ? "" : ";noHA") + (c.LeagueMeanPrior ? "" : ";no-lmp");
        void AddD(DavidsonEloConfig c, bool count = true)
        {
            var n = DName(c);
            if (davidson.ContainsKey(n)) return;
            davidson[n] = RunDavidson(c); davidsonCfg[n] = c;
            if (count) configs.Add(("C1-Davidson", n, c.Hash));
        }
        var baseGrid = (from k in new[] { 16.0, 24.0, 32.0 } from carry in new[] { 1.0, 0.8 } from promo in new[] { 0.0, 60.0 }
                        select new DavidsonEloConfig { K = k, SeasonCarry = carry, PromotionPrior = promo }).ToList();
        foreach (var c in baseGrid) AddD(c);

        // Aday 2 — HİYERARŞİK HÜCUM/SAVUNMA (4.0 Poisson motorunun genişletmesi): takım iç saha etkisi (hücum+savunma, lig
        // ortalamasına çekilir) ve Dixon–Coles ρ. Model 4'ün seçilmiş kalibrasyonu aynen kullanılır.
        var poisson = new Dictionary<string, IReadOnlyDictionary<int, Probs3>>();
        IReadOnlyDictionary<int, Probs3> RunPoisson(double edge, double rho, Func<OutcomeExpectation, OutcomeExpectation>? ablate = null)
        {
            var p = chosen.Clone(); p.TeamHomeEdgeRate = edge; p.LowScoreRho = rho;
            var model = new OutcomeRatingModel(p, catalog);
            var d = new Dictionary<int, Probs3>();
            var i = 0;
            while (i < history.Count && history[i].KickoffUtc < Cutoff)
            {
                var j = i; while (j < history.Count && history[j].KickoffUtc == history[i].KickoffUtc) j++;
                for (var k = i; k < j; k++)
                {
                    var m = history[k];
                    if (!S0.ContainsKey(m.MatchId)) continue;
                    var e = model.Expect(m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.KickoffUtc);
                    if (ablate != null) e = ablate(e);
                    d[m.MatchId] = Probs3.From(OutcomePredictor.Predict(e, m.LeagueId, p).Calibrated);
                }
                for (var k = i; k < j; k++) model.Update(history[k]);
                i = j;
            }
            return d;
        }
        // Tutarlılık: genişletmesiz yeniden oynatma, dondurulmuş Model 4 ile birebir aynı olmalı.
        var replay40 = RunPoisson(0, 0);
        var replayMatches = manifest.All(id => replay40.TryGetValue(id, out var p) && Math.Abs(p.H - m4[id].H) < 1e-9 && Math.Abs(p.D - m4[id].D) < 1e-9);
        foreach (var (edge, rho) in new[] { (0.01, 0.0), (0.02, 0.0), (0.0, -0.05), (0.01, -0.05) })
        {
            var n = string.Create(inv, $"edge={edge};rho={rho}");
            poisson[n] = RunPoisson(edge, rho);
            configs.Add(("C2-HierPoisson", n, n));
        }

        // Aday 3 — rakip düzeltilmiş form (Davidson motoruna eklenir; en iyi taban konfigürasyon üzerinde iç seçimle)
        var formWeights = new[] { 0.5, 1.0 };
        const double restBeta = 5;

        // ════════════════ D) NESTED WALK-FORWARD ════════════════
        double LL(IEnumerable<int> ids, IReadOnlyDictionary<int, Probs3> m) { double s = 0; var n = 0; foreach (var id in ids) { s += Model6Lab.LogLoss(m[id], S0[id].Y); n++; } return n == 0 ? double.NaN : s / n; }
        var foldRecords = new List<Dictionary<string, object?>>();
        var finalPred = new Dictionary<string, Dictionary<int, Probs3>>(); // aile adı → dış fold tahminleri (manifest)
        foreach (var name in new[] { "Frekans", "Elo-mevcut(bağımsız)", "Elo-basit(dahili)", "Poisson-ham(4.0)", "Model4", "C1-Davidson", "C2-HierPoisson", "C3-Davidson+Form", "C4-Ensemble", "Model6", "Model6+Kalibrasyon" })
            finalPred[name] = new Dictionary<int, Probs3>();
        var innerSplitsOk = true;

        // Aynı prosedür: iç pencere → seçilmiş yapılandırma. Final dondurma için kesime kadar da uygulanır.
        (Func<int, Probs3> Model6, Func<int, Probs3> Calibrated, Dictionary<string, object?> Info, Func<int, Probs3> C1, Func<int, Probs3> C2, Func<int, Probs3> C3, Func<int, Probs3> C4,
            double EloExistC, double EloInternalC, DavidsonEloConfig DCfg, Func<IReadOnlyDictionary<int, Probs3>, Func<int, Probs3>> C4Swap) Select(List<int> inner)
        {
            var info = new Dictionary<string, object?>();
            var m4Inner = LL(inner, m4);
            info["innerN"] = inner.Count; info["innerM4"] = m4Inner;
            var cE = eloC.OrderBy(c => LL(inner, eloExistingBy[c])).First();
            var cI = eloC.OrderBy(c => LL(inner, eloInternalBy[c])).First();
            // C1: taban ızgaradan en iyi (iç LL)
            var baseName = baseGrid.Select(DName).OrderBy(n => LL(inner, davidson[n])).First();
            var best = davidsonCfg[baseName]; var bestLoss = LL(inner, davidson[baseName]);
            info["c1Base"] = baseName; info["c1BaseInner"] = bestLoss;
            // C3: form yalnız iç kazanç ≥ eşikse
            var c3 = best; var c3Loss = bestLoss;
            foreach (var fw in formWeights)
            {
                var cfg = best with { FormWeight = fw }; AddD(cfg);
                var l = LL(inner, davidson[DName(cfg)]);
                if (l < c3Loss - MinGain) { c3 = cfg; c3Loss = l; }
            }
            {
                var cfg = c3 with { RestBeta = restBeta }; AddD(cfg);
                var l = LL(inner, davidson[DName(cfg)]);
                if (l < c3Loss - MinGain) { c3 = cfg; c3Loss = l; }
            }
            info["c3"] = DName(c3); info["c3Inner"] = c3Loss;
            var dBest = c3Loss < bestLoss ? c3 : best; // Davidson ailesinin iç seçimi
            var dPred = davidson[DName(dBest)];
            // C2: Model 4'ü iç pencerede eşik kadar geçmezse Model 4 kalır
            var pName = poisson.Keys.OrderBy(n => LL(inner, poisson[n])).First();
            var pLoss = LL(inner, poisson[pName]);
            IReadOnlyDictionary<int, Probs3> c2Pred = pLoss < m4Inner - MinGain ? poisson[pName] : m4;
            info["c2"] = pLoss < m4Inner - MinGain ? pName : "Model4 (kazanç yok: " + pName + ")"; info["c2Inner"] = pLoss;
            // C4: ensemble — taban Model 4; Davidson ve (kanıtlanmışsa) C2 doğrusal havuz. Segment (UEFA / yerel) ağırlıkları iç
            // pencerede, küçük segment global ağırlığa daraltılır; iç kazanç eşiği altında ağırlık 0.
            var wDs = new[] { 0.0, 0.2, 0.35, 0.5 };
            var wPs = ReferenceEquals(c2Pred, m4) ? new[] { 0.0 } : new[] { 0.0, 0.2 };
            (double WD, double WP) BestW(List<int> ids)
            {
                (double, double) bw = (0, 0); var bl = LL(ids, m4);
                foreach (var wd in wDs) foreach (var wp in wPs)
                {
                    if (wd + wp == 0) continue;
                    var l = ids.Average(id => Model6Lab.LogLoss(Model6Lab.Pool(m4[id], new[] { (dPred[id], wd), (c2Pred[id], wp) }), S0[id].Y));
                    if (l < bl - 1e-12) { bl = l; bw = (wd, wp); }
                }
                return bw;
            }
            var wAll = BestW(inner);
            var seg = new Dictionary<bool, (double WD, double WP)>();
            foreach (var isUefa in new[] { true, false })
            {
                var ids = inner.Where(id => Uefa.Contains(S0[id].LeagueId) == isUefa).ToList();
                var ws = ids.Count == 0 ? wAll : BestW(ids);
                double sh(double a, double g) => Math.Round((ids.Count * a + SegmentMinSample * g) / (ids.Count + SegmentMinSample), 3);
                seg[isUefa] = ids.Count < SegmentMinSample / 3 ? wAll : (sh(ws.WD, wAll.WD), sh(ws.WP, wAll.WP));
            }
            Probs3 C4(int id) { var w = seg[Uefa.Contains(S0[id].LeagueId)]; return Model6Lab.Pool(m4[id], new[] { (dPred[id], w.WD), (c2Pred[id], w.WP) }); }
            var c4Loss = inner.Average(id => Model6Lab.LogLoss(C4(id), S0[id].Y));
            var c4On = c4Loss < m4Inner - MinGain;
            info["c4"] = new { all = wAll, uefa = seg[true], domestic = seg[false], inner = c4Loss, on = c4On };
            // Model 6 = iç pencerede en düşük LL veren aile (Model 4'ü eşik kadar geçemezse Model 4).
            var fams = new List<(string Name, double Loss, Func<int, Probs3> F)>
            {
                ("C1-Davidson", bestLoss, id => davidson[DName(best)][id]),
                ("C3-Davidson+Form", c3Loss, id => davidson[DName(c3)][id]),
                ("C2-HierPoisson", pLoss, id => poisson[pName][id]),
                ("C4-Ensemble", c4On ? c4Loss : double.MaxValue, C4)
            };
            var win = fams.OrderBy(f => f.Loss).First();
            Func<int, Probs3> m6 = win.Loss < m4Inner - MinGain ? win.F : id => m4[id];
            info["model6"] = win.Loss < m4Inner - MinGain ? win.Name : "Model4"; info["model6Inner"] = Math.Min(win.Loss, m4Inner);
            // H) Kalibrasyon — yalnız iç pencerede uydurulur; iç kazanç eşiği altında uygulanmaz.
            var innerPairs = inner.Select(id => (m6(id), S0[id].Y)).ToList();
            var t = Model6Lab.FitTemperature(innerPairs);
            var v = Model6Lab.FitVector(innerPairs);
            var lT = innerPairs.Average(x => Model6Lab.LogLoss(Model6Lab.Temperature(x.Item1, t), x.Y));
            var lV = innerPairs.Average(x => Model6Lab.LogLoss(Model6Lab.Vector(x.Item1, v.T, v.BDraw, v.BAway), x.Y));
            var l0 = innerPairs.Average(x => Model6Lab.LogLoss(x.Item1, x.Y));
            Func<int, Probs3> cal = m6; var calName = "yok";
            if (Math.Min(lT, lV) < l0 - MinGain)
            {
                if (lV < lT - MinGain) { cal = id => Model6Lab.Vector(m6(id), v.T, v.BDraw, v.BAway); calName = string.Create(inv, $"vector T={v.T} bD={v.BDraw} bA={v.BAway}"); }
                else { cal = id => Model6Lab.Temperature(m6(id), t); calName = string.Create(inv, $"temperature T={t}"); }
            }
            info["calibration"] = new { temperature = t, vector = v, inner0 = l0, innerT = lT, innerV = lV, chosen = calName };
            Func<int, Probs3> Swap(IReadOnlyDictionary<int, Probs3> alt)
                => id => { var w = seg[Uefa.Contains(S0[id].LeagueId)]; return Model6Lab.Pool(m4[id], new[] { (alt[id], w.WD), (c2Pred[id], w.WP) }); };
            info["c4Davidson"] = DName(dBest);
            return (m6, cal, info, id => davidson[DName(best)][id], id => c2Pred[id], id => davidson[DName(c3)][id], C4, cE, cI, dBest, Swap);
        }

        var foldSels = new List<(DavidsonEloConfig DCfg, Func<int, Probs3> C4, Func<IReadOnlyDictionary<int, Probs3>, Func<int, Probs3>> Swap, List<int> Outer)>();
        for (var k = 0; k < folds.Count; k++)
        {
            var (from, to) = folds[k];
            var inner = evalSamples.Where(s => s.KickoffUtc < from).Select(s => s.MatchId).ToList();
            var outer = manifest.Where(id => S0[id].KickoffUtc >= from && S0[id].KickoffUtc < to).ToList();
            innerSplitsOk &= !inner.ToHashSet().Overlaps(outer) && inner.All(id => S0[id].KickoffUtc < from) && outer.All(id => S0[id].KickoffUtc >= from);
            var sel = Select(inner);
            foreach (var id in outer)
            {
                finalPred["Frekans"][id] = freq[id];
                finalPred["Elo-mevcut(bağımsız)"][id] = eloExistingBy[sel.EloExistC][id];
                finalPred["Elo-basit(dahili)"][id] = eloInternalBy[sel.EloInternalC][id];
                finalPred["Poisson-ham(4.0)"][id] = m4Raw[id];
                finalPred["Model4"][id] = m4[id];
                finalPred["C1-Davidson"][id] = sel.C1(id);
                finalPred["C2-HierPoisson"][id] = sel.C2(id);
                finalPred["C3-Davidson+Form"][id] = sel.C3(id);
                finalPred["C4-Ensemble"][id] = sel.C4(id);
                finalPred["Model6"][id] = sel.Model6(id);
                finalPred["Model6+Kalibrasyon"][id] = sel.Calibrated(id);
            }
            sel.Info["fold"] = k + 1; sel.Info["from"] = from; sel.Info["to"] = to; sel.Info["outerN"] = outer.Count;
            foldRecords.Add(sel.Info);
            foldSels.Add((sel.DCfg, sel.C4, sel.C4Swap, outer));
        }

        // ── Sonuç tabloları ──
        sb.AppendLine("\n## D) Dış fold'lar (iç pencere = [2024-09-05, fold başı), genişleyen)");
        sb.AppendLine($"bölmeler ayrık ve zaman yönü doğru: {innerSplitsOk}; Model 4 yeniden oynatma = dondurulmuş Model 4: {replayMatches}");
        sb.AppendLine("| Fold | Tarih | İç N | Dış N | Seçilen Model 6 | C1 taban | C3 | C2 | C4 ağırlık | Kalibrasyon |\n|---|---|---|---|---|---|---|---|---|---|");
        foreach (var f in foldRecords)
            sb.AppendLine($"| {f["fold"]} | {(DateTime)f["from"]!:yyyy-MM-dd}→{(DateTime)f["to"]!:yyyy-MM-dd} | {f["innerN"]} | {f["outerN"]} | {f["model6"]} | {f["c1Base"]} | {f["c3"]} | {f["c2"]} | {JsonSerializer.Serialize(f["c4"], new JsonSerializerOptions { IncludeFields = true })} | {JsonSerializer.Serialize(f["calibration"], new JsonSerializerOptions { IncludeFields = true })} |");

        sb.AppendLine("\n## E/F) Nested walk-forward sonuçları (dış fold tahminleri, manifest)");
        sb.AppendLine("| Model | LL | Brier | ECE | Acc | Draw P/R | Fold LL ort | Fold LL medyan | M4'ü geçtiği fold | ΔLL vs M4 [%95 blok CI] | ΔBrier [CI] |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        var summary = new Dictionary<string, object>();
        var stats = new Dictionary<string, (int Wins, double FoldMean, double FoldMeanM4)>();
        foreach (var (name, pred) in finalPred)
        {
            var mt = Model6Lab.Evaluate(Pairs(manifest, pred));
            var foldLL = folds.Select(f => LL(manifest.Where(id => S0[id].KickoffUtc >= f.From && S0[id].KickoffUtc < f.To), pred)).ToList();
            var foldM4 = folds.Select(f => LL(manifest.Where(id => S0[id].KickoffUtc >= f.From && S0[id].KickoffUtc < f.To), m4)).ToList();
            var wins = foldLL.Zip(foldM4).Count(z => z.First < z.Second - 1e-12);
            var ci = OutcomeAccuracyLab.PairedBlockBootstrap(manifest.Select(id => (S0[id].KickoffUtc, Model6Lab.LogLoss(pred[id], S0[id].Y) - Model6Lab.LogLoss(m4[id], S0[id].Y))).ToList());
            var bci = OutcomeAccuracyLab.PairedBlockBootstrap(manifest.Select(id => (S0[id].KickoffUtc, Model6Lab.Brier(pred[id], S0[id].Y) - Model6Lab.Brier(m4[id], S0[id].Y))).ToList());
            var sorted = foldLL.OrderBy(x => x).ToList();
            var median = (sorted[(sorted.Count - 1) / 2] + sorted[sorted.Count / 2]) / 2;
            summary[name] = new { metrics = mt, foldLL, foldM4, wins, ci, bci, foldMean = foldLL.Average(), foldMedian = median };
            stats[name] = (wins, foldLL.Average(), foldM4.Average());
            sb.AppendLine($"| {name} | {mt.LogLoss:0.00000} | {mt.Brier:0.00000} | {mt.Ece:0.00000} | {mt.Accuracy:P2} | {mt.Precision[1]:0.000}/{mt.Recall[1]:0.000} | {foldLL.Average():0.00000} | {median:0.00000} | {wins}/{folds.Count} | {ci.Mean:+0.00000;-0.00000} [{ci.Low:+0.00000;-0.00000}, {ci.High:+0.00000;-0.00000}] | {bci.Mean:+0.00000;-0.00000} [{bci.Low:+0.00000;-0.00000}, {bci.High:+0.00000;-0.00000}] |");
        }
        sb.AppendLine("\nFold bazında LL (Model 4 / Model 6 / C1 / C4):");
        for (var k = 0; k < folds.Count; k++)
        {
            var ids = manifest.Where(id => S0[id].KickoffUtc >= folds[k].From && S0[id].KickoffUtc < folds[k].To).ToList();
            sb.AppendLine($"- fold {k + 1} (n={ids.Count}): {LL(ids, m4):0.00000} / {LL(ids, finalPred["Model6"]):0.00000} / {LL(ids, finalPred["C1-Davidson"]):0.00000} / {LL(ids, finalPred["C4-Ensemble"]):0.00000}");
        }

        // ════════════════ G) ABLASYON (Davidson motoru; kesimdeki seçilmiş yapı üzerinde, dış fold'larda) ════════════════
        var finalSel = Select(evalSamples.Select(s => s.MatchId).ToList());
        var refName = (string)finalSel.Info["c3"]!;
        var refCfg = davidsonCfg[refName];
        var ablations = new List<(string Component, DavidsonEloConfig Cfg)>
        {
            ("Ev sahibi avantajı", refCfg with { HomeAdvantage = false }),
            ("Beraberlik parametresi (lig ν)", refCfg with { LeagueDraw = false }),
            ("Rakip düzeltilmiş form", refCfg with { FormWeight = refCfg.FormWeight == 0 ? 0.5 : 0 }),
            ("Recency decay (form)", refCfg with { FormRecency = !refCfg.FormRecency, FormWeight = refCfg.FormWeight == 0 ? 0.5 : refCfg.FormWeight }),
            ("Sezon geçiş prior'ı", refCfg with { SeasonCarry = refCfg.SeasonCarry < 1 ? 1.0 : 0.8 }),
            ("Yükselen takım prior'ı", refCfg with { PromotionPrior = refCfg.PromotionPrior > 0 ? 0 : 60 }),
            ("Lig gücü (lig ortalaması önseli)", refCfg with { LeagueMeanPrior = false }),
            ("Rest/congestion", refCfg with { RestBeta = refCfg.RestBeta == 0 ? restBeta : 0 })
        };
        sb.AppendLine($"\n## G) Ablasyon — referans Davidson yapı: {refName} (kesimdeki iç seçim). Satır: bileşen DEĞİŞTİRİLİNCE Δ (+ = kötüleşme, yani bileşen katkı sağlıyor)");
        sb.AppendLine("| Bileşen | Referansta | ΔLL dış (ort) | Katkı sağladığı fold | ΔBrier |\n|---|---|---|---|---|");
        var refPred = davidson[refName];
        var ablRows = new List<object>();
        foreach (var (comp, cfg) in ablations)
        {
            AddD(cfg, count: false);
            var ap = davidson[DName(cfg)];
            var present = comp switch
            {
                "Ev sahibi avantajı" => refCfg.HomeAdvantage, "Beraberlik parametresi (lig ν)" => refCfg.LeagueDraw, "Rakip düzeltilmiş form" => refCfg.FormWeight > 0,
                "Recency decay (form)" => refCfg.FormWeight > 0 && refCfg.FormRecency, "Sezon geçiş prior'ı" => refCfg.SeasonCarry < 1, "Yükselen takım prior'ı" => refCfg.PromotionPrior > 0,
                "Lig gücü (lig ortalaması önseli)" => refCfg.LeagueMeanPrior, _ => refCfg.RestBeta != 0
            };
            // present: referans bileşeni içeriyor → varyant = bileşensiz; Δ = varyant − referans (+ → bileşen faydalı)
            // absent: referans içermiyor → varyant = bileşenli; Δ = referans − varyant (+ → bileşen faydalı olurdu)
            var perFold = folds.Select(f =>
            {
                var ids = manifest.Where(id => S0[id].KickoffUtc >= f.From && S0[id].KickoffUtc < f.To).ToList();
                return present ? LL(ids, ap) - LL(ids, refPred) : LL(ids, refPred) - LL(ids, ap);
            }).ToList();
            var dB = manifest.Average(id => Model6Lab.Brier(ap[id], S0[id].Y) - Model6Lab.Brier(refPred[id], S0[id].Y)) * (present ? 1 : -1);
            var helps = perFold.Count(v => v > 0);
            ablRows.Add(new { comp, present, perFold, helps, dB });
            sb.AppendLine($"| {comp} | {(present ? "var" : "yok")} | {perFold.Average():+0.00000;-0.00000} | {helps}/{folds.Count} | {dB:+0.00000;-0.00000} |");
        }
        // Model 4 tabanı üzerinde hücum / savunma ablasyonu (Poisson ailesi)
        foreach (var (comp, fn) in new (string, Func<OutcomeExpectation, OutcomeExpectation>)[]
                 {
                     ("Hücum gücü (Poisson)", e => e with { LambdaHome = Math.Clamp(e.LambdaHome * Math.Exp(-e.HomeLogAttack), 0.15, 4.5), LambdaAway = Math.Clamp(e.LambdaAway * Math.Exp(-e.AwayLogAttack), 0.15, 4.5) }),
                     ("Savunma gücü (Poisson)", e => e with { LambdaHome = Math.Clamp(e.LambdaHome * Math.Exp(-e.AwayLogDefence), 0.15, 4.5), LambdaAway = Math.Clamp(e.LambdaAway * Math.Exp(-e.HomeLogDefence), 0.15, 4.5) })
                 })
        {
            var ap = RunPoisson(0, 0, fn);
            var perFold = folds.Select(f =>
            {
                var ids = manifest.Where(id => S0[id].KickoffUtc >= f.From && S0[id].KickoffUtc < f.To).ToList();
                return LL(ids, ap) - LL(ids, m4);
            }).ToList();
            var helps = perFold.Count(v => v > 0);
            ablRows.Add(new { comp, present = true, perFold, helps });
            sb.AppendLine($"| {comp} | var (Model 4) | {perFold.Average():+0.00000;-0.00000} | {helps}/{folds.Count} | – |");
        }

        // ── G2) ENSEMBLE İÇİ ablasyon — dondurulacak aile (C4) üzerinde, her dış fold'un KENDİ seçtiği Davidson yapısı ve ağırlıklarıyla.
        //     Bileşen yalnız Davidson tahminlerinde değiştirilir; ağırlıklar aynı kalır (yeniden seçim yok).
        var toggles = new List<(string Comp, Func<DavidsonEloConfig, DavidsonEloConfig> Toggle, Func<DavidsonEloConfig, bool> Present)>
        {
            ("Ev sahibi avantajı", c => c with { HomeAdvantage = !c.HomeAdvantage }, c => c.HomeAdvantage),
            ("Beraberlik parametresi (lig ν)", c => c with { LeagueDraw = !c.LeagueDraw }, c => c.LeagueDraw),
            ("Rakip düzeltilmiş form", c => c with { FormWeight = c.FormWeight == 0 ? 0.5 : 0 }, c => c.FormWeight > 0),
            ("Sezon geçiş prior'ı", c => c with { SeasonCarry = c.SeasonCarry < 1 ? 1.0 : 0.8 }, c => c.SeasonCarry < 1),
            ("Yükselen takım prior'ı", c => c with { PromotionPrior = c.PromotionPrior > 0 ? 0 : 60 }, c => c.PromotionPrior > 0),
            ("Lig gücü (lig ortalaması önseli)", c => c with { LeagueMeanPrior = !c.LeagueMeanPrior }, c => c.LeagueMeanPrior),
            ("Rest/congestion", c => c with { RestBeta = c.RestBeta == 0 ? restBeta : 0 }, c => c.RestBeta != 0)
        };
        sb.AppendLine("\n### G2) Ensemble içi ablasyon (C4; fold'un kendi Davidson yapısı + ağırlıkları). + = bileşen katkı sağlıyor / sağlardı");
        sb.AppendLine("| Bileşen | Fold'larda var | ΔLL fold'lar | Katkı fold | Kural (≥3 fold) |\n|---|---|---|---|---|");
        var ensAbl = new List<object>();
        foreach (var (comp, toggle, present) in toggles)
        {
            var perFold = new List<double>(); var presentIn = 0;
            foreach (var fs in foldSels)
            {
                var alt = toggle(fs.DCfg); AddD(alt, count: false);
                var full = fs.C4; var abl = fs.Swap(davidson[DName(alt)]);
                double l(Func<int, Probs3> f) => fs.Outer.Average(id => Model6Lab.LogLoss(f(id), S0[id].Y));
                var isPresent = present(fs.DCfg);
                if (isPresent) presentIn++;
                perFold.Add(isPresent ? l(abl) - l(full) : l(full) - l(abl));
            }
            var helps = perFold.Count(v => v > 0);
            ensAbl.Add(new { comp, presentIn, perFold, helps });
            sb.AppendLine($"| {comp} | {presentIn}/{foldSels.Count} | {string.Join(" ", perFold.Select(v => v.ToString("+0.00000;-0.00000", inv)))} | {helps}/{foldSels.Count} | {(helps >= 3 ? "katkı var" : "katkı yok")} |");
        }

        // ════════════════ I) TARİHSEL KABUL KAPISI — her aday aynı kapıdan geçer ════════════════
        var jsonOpts = new JsonSerializerOptions { IncludeFields = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        var gateResults = new Dictionary<string, object>();
        var gateText = new StringBuilder();
        (bool Pass, List<int> Bad) Gate(string label, IReadOnlyDictionary<int, Probs3> pred)
        {
            var mm = Model6Lab.Evaluate(Pairs(manifest, pred));
            var foldLL = folds.Select(f => LL(manifest.Where(id => S0[id].KickoffUtc >= f.From && S0[id].KickoffUtc < f.To), pred)).ToList();
            var foldM4 = folds.Select(f => LL(manifest.Where(id => S0[id].KickoffUtc >= f.From && S0[id].KickoffUtc < f.To), m4)).ToList();
            var wins = foldLL.Zip(foldM4).Count(z => z.First < z.Second - 1e-12);
            var ci = OutcomeAccuracyLab.PairedBlockBootstrap(manifest.Select(id => (S0[id].KickoffUtc, Model6Lab.LogLoss(pred[id], S0[id].Y) - Model6Lab.LogLoss(m4[id], S0[id].Y))).ToList());
            var notWorse = 0; var bad = new List<int>(); var rows = new List<object>();
            var t = gateText;
            t.AppendLine($"\n### {label}");
            t.AppendLine("| Org | N | M4 LL | Aday LL | Δ | M4 Brier | Aday Brier | M4 ECE | Aday ECE | M4 Acc | Aday Acc |\n|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var o in Orgs)
            {
                var ids = manifest.Where(id => S0[id].LeagueId == o).ToList();
                var a = Model6Lab.Evaluate(Pairs(ids, m4)); var b = Model6Lab.Evaluate(Pairs(ids, pred));
                var d = b.LogLoss - a.LogLoss;
                if (d <= 0) notWorse++;
                if (d >= 0.01) bad.Add(o);
                rows.Add(new { org = o, n = ids.Count, m4 = a, cand = b, d });
                t.AppendLine($"| {o} | {ids.Count} | {a.LogLoss:0.00000} | {b.LogLoss:0.00000} | {d:+0.00000;-0.00000} | {a.Brier:0.00000} | {b.Brier:0.00000} | {a.Ece:0.0000} | {b.Ece:0.0000} | {a.Accuracy:P1} | {b.Accuracy:P1} |");
            }
            var crossIds = manifest.Where(id => S0[id].Cross).ToList();
            var classOk = Enumerable.Range(0, 3).All(c => (double.IsNaN(m4Test.Recall[c]) || mm.Recall[c] >= m4Test.Recall[c] - 0.02)
                                                         && Math.Abs(mm.MeanProbability[c] - mm.ActualRate[c]) <= Math.Abs(m4Test.MeanProbability[c] - m4Test.ActualRate[c]) + 0.01);
            var g = new Dictionary<string, bool>
            {
                ["foldOrtLL<M4"] = foldLL.Average() < foldM4.Average(),
                [$"≥{Math.Ceiling(OuterFoldCount * 0.8)}/{OuterFoldCount} fold"] = wins >= Math.Ceiling(OuterFoldCount * 0.8),
                ["CI üst<0"] = ci.High < 0,
                ["Brier kötüleşmedi"] = mm.Brier <= m4Test.Brier,
                ["ECE ≤ M4+0,005"] = mm.Ece <= m4Test.Ece + 0.005,
                ["Acc ≥ M4"] = mm.Accuracy >= m4Test.Accuracy,
                ["sınıf ezilmedi"] = classOk,
                ["kapsam aynı"] = pred.Count(kv => testSet.Contains(kv.Key)) == manifest.Count,
                ["≥7/11 org kötüleşmedi"] = notWorse >= 7,
                ["hiçbir org +0,01"] = bad.Count == 0
            };
            var pass = g.Values.All(v => v);
            t.AppendLine($"ligler arası n={crossIds.Count}: M4 {LL(crossIds, m4):0.00000} → aday {LL(crossIds, pred):0.00000}");
            t.AppendLine($"LL {m4Test.LogLoss:0.00000} → {mm.LogLoss:0.00000}; Brier {m4Test.Brier:0.00000} → {mm.Brier:0.00000}; ECE {m4Test.Ece:0.00000} → {mm.Ece:0.00000}; Acc {m4Test.Accuracy:P2} → {mm.Accuracy:P2}; beraberlik P/R {m4Test.Precision[1]:0.000}/{m4Test.Recall[1]:0.000} → {mm.Precision[1]:0.000}/{mm.Recall[1]:0.000}");
            t.AppendLine($"fold ort {foldM4.Average():0.00000} → {foldLL.Average():0.00000}; geçtiği fold {wins}/{OuterFoldCount}; ΔLL {ci.Mean:+0.00000;-0.00000} [{ci.Low:+0.00000;-0.00000}, {ci.High:+0.00000;-0.00000}]; org kötüleşmeyen {notWorse}/11; +0,01: {string.Join(",", bad)}");
            t.AppendLine("kapılar: " + string.Join("; ", g.Select(kv => $"{kv.Key} {(kv.Value ? "✓" : "✗")}")) + $" → {(pass ? "GEÇTİ" : "GEÇMEDİ")}");
            gateResults[label] = new { metrics = mm, foldLL, foldM4, wins, ci, notWorse, bad, rows, gates = g, pass };
            return (pass, bad);
        }
        IReadOnlyDictionary<int, Probs3> Revert(IReadOnlyDictionary<int, Probs3> pred, Func<int, bool> toM4)
            => manifest.ToDictionary(id => id, id => toM4(id) ? m4[id] : pred[id]);

        var gM6 = Gate("Model 6 = nested prosedür (ana karar)", finalPred["Model6"]);
        var gC4 = Gate("C4-Ensemble (kesimde dondurulacak aile)", finalPred["C4-Ensemble"]);
        // Kullanıcı kapısı: +0,01 ya da daha fazla kötüleşen organizasyonda aday uygulanmaz (Model 4 kalır) — sonra bütün kapılar yeniden.
        var gM6r = gM6.Bad.Count == 0 ? gM6 : Gate($"Model 6 + org kuralı ({string.Join(",", gM6.Bad)} → Model 4)", Revert(finalPred["Model6"], id => gM6.Bad.Contains(S0[id].LeagueId)));
        if (gC4.Bad.Count > 0) Gate($"C4 + org kuralı ({string.Join(",", gC4.Bad)} → Model 4)", Revert(finalPred["C4-Ensemble"], id => gC4.Bad.Contains(S0[id].LeagueId)));
        // DUYARLILIK (karar dayanağı DEĞİL): 26.09'da ön-kaydedilen yapısal kural — Elo ensemble'ı UEFA ve ligler arası maçta uygulanmaz.
        // Bu tur sonuçları görüldükten sonra hesaplandığı için yalnız bilgi amaçlıdır.
        Gate("DUYARLILIK: C4 yalnız yerel lig (26.09 ön-kayıtlı kural; sonuç görüldükten sonra hesaplandı)", Revert(finalPred["C4-Ensemble"], id => Uefa.Contains(S0[id].LeagueId) || S0[id].Cross));
        // REFERANS: Model 5 gölge (parametreleri bu final test görüldükten sonra seçildi — kanıt değildir).
        var m5 = evalSamples.Where(s => testSet.Contains(s.MatchId)).ToDictionary(s => s.MatchId,
            s => Probs3.From(Model5Shadow.Predict(OutcomePredictor.Predict(s.E, s.LeagueId, chosen).Calibrated, s.LeagueId, s.E.CrossLeague, eloExisting[s.MatchId])));
        Gate("REFERANS: Model 5 gölge (test görülmüş; kanıt değil)", m5);
        var pass = gM6.Pass;
        sb.AppendLine("\n## I) Tarihsel kabul kapısı");
        sb.Append(gateText);
        sb.AppendLine($"\nSONUÇ (ana karar = nested prosedür): {(gM6.Pass ? "GEÇTİ" : gM6r.Pass ? "YALNIZ ORG KURALIYLA GEÇTİ (PARTIAL)" : "GEÇMEDİ")}");

        sb.AppendLine($"\n## Kesimdeki (dondurma adayı) seçim: {JsonSerializer.Serialize(finalSel.Info, jsonOpts)}");
        sb.AppendLine($"\nkonfigürasyon sayısı: aday motor {configs.Count} + ensemble ağırlık ızgarası 7 + kalibrasyon yöntemi 2 = {configs.Count + 9}; süre {sw.ElapsedMilliseconds} ms");

        await File.WriteAllTextAsync(Path.Combine(outDir, "model6-lab.md"), sb.ToString());
        await File.WriteAllTextAsync(Path.Combine(outDir, "model6-lab.json"), JsonSerializer.Serialize(new
        {
            json, folds, foldRecords, summary, ablRows, ensAbl, gateResults, pass, configs = configs.Select(c => new { c.Family, c.Name, c.Hash }),
            finalSelection = finalSel.Info, replayMatches, innerSplitsOk, manifest
        }, jsonOpts));

        Assert.True(m4Deterministic);
        Assert.True(replayMatches);
        Assert.True(innerSplitsOk);
        Assert.True(configs.Count + 9 <= 40);
        // Dondurulmuş Model 6 gölge = kesimdeki iç seçim (Davidson yapısı + segment ağırlıkları).
        Assert.Equal(DName(Model6Shadow.Davidson), (string)finalSel.Info["c4Davidson"]!);
        var c4Json = JsonSerializer.Serialize(finalSel.Info["c4"], jsonOpts);
        Assert.Contains("\"domestic\":{\"Item1\":" + Model6Shadow.DomesticWeight.ToString(inv) + ",\"Item2\":0}", c4Json);
        Assert.Contains("\"uefa\":{\"Item1\":" + Model6Shadow.UefaWeight.ToString(inv) + ",\"Item2\":0}", c4Json);
        Assert.All(finalPred.Values.SelectMany(p => p.Values), p => Assert.True(p.IsValid));
    }

    private static string MetricsLine(string name, Model6Lab.Metrics m)
        => $"{name}: N={m.N} Acc={m.Accuracy:P2} LL={m.LogLoss:0.00000} Brier={m.Brier:0.00000} ECE={m.Ece:0.00000}; ort. tahmin E/B/D {Join(m.MeanProbability)} gerçek {Join(m.ActualRate)}";

    private static string Join(double[] v) => string.Join("/", v.Select(x => x.ToString("0.000", CultureInfo.InvariantCulture)));
}
