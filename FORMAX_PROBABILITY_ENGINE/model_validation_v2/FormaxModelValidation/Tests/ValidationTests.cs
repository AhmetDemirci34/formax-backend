using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.ModelValidation.Services;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;
using Formax.TeamStrength.Tests;

namespace Formax.ModelValidation.Tests;

public static class ValidationTests
{
    private static DixonColesConfig Dc(double rho = 0.0, int maxGoals = 10) => new()
    { Rho = rho, MaxGoals = maxGoals, MinLambda = 0.05, MaxLambda = 6.0, ProbabilityFloor = 1e-6 };

    private static SplitConfig Split() => SplitConfig.Load("(none)");

    private static MatchRecord M(string id, string date, string home, string away, int hg, int ag,
        string type = "DOMESTIC_LEAGUE") => new()
        {
            MatchId = id,
            Date = DateOnly.ParseExact(date, "yyyy-MM-dd"),
            Season = "2020/21",
            Competition = "Premier League",
            CompetitionType = type,
            HomeTeamId = home, AwayTeamId = away,
            HomeTeamName = home, AwayTeamName = away,
            HomeGoals = hg, AwayGoals = ag,
            MatchStatus = "FT", IdentityConfidence = "CONFIRMED"
        };

    public static void Register(TestRunner r, string? datasetPath, string? snapshotPath,
        string? splitPath, string? dcConfigPath, string? tsConfigPath)
    {
        // ---------------------------------------------------------------- bivariate Poisson maths

        r.Add("bivariate Poisson with lambda3 = 0 is exactly independent Poisson", () =>
        {
            var cfg = Dc();
            foreach (var (lh, la) in new[] { (1.6, 1.1), (0.4, 2.7), (3.1, 0.2) })
            {
                var bp = BivariatePoissonModel.Outcome1X2(lh, la, 0.0, cfg);
                var ip = DixonColesModel.Outcome1X2(lh, la, cfg);
                TestRunner.Equal(ip.Home, bp.Home, 1e-12, "home");
                TestRunner.Equal(ip.Draw, bp.Draw, 1e-12, "draw");
                TestRunner.Equal(ip.Away, bp.Away, 1e-12, "away");
            }
        });

        r.Add("bivariate Poisson keeps the marginal means it was given", () =>
        {
            var cfg = Dc(maxGoals: 30);
            const double lh = 1.7, la = 1.2, l3 = 0.35;
            var g = BivariatePoissonModel.ScoreGrid(lh, la, l3, cfg);
            double ex = 0, ey = 0;
            for (var x = 0; x < g.Length; x++)
                for (var y = 0; y < g[x].Length; y++) { ex += x * g[x][y]; ey += y * g[x][y]; }
            TestRunner.Equal(lh, ex, 1e-9, "E[home goals]");
            TestRunner.Equal(la, ey, 1e-9, "E[away goals]");
        });

        r.Add("bivariate Poisson covariance equals its shared component", () =>
        {
            var cfg = Dc(maxGoals: 30);
            const double lh = 1.7, la = 1.2, l3 = 0.35;
            var g = BivariatePoissonModel.ScoreGrid(lh, la, l3, cfg);
            double ex = 0, ey = 0, exy = 0;
            for (var x = 0; x < g.Length; x++)
                for (var y = 0; y < g[x].Length; y++)
                { ex += x * g[x][y]; ey += y * g[x][y]; exy += x * y * g[x][y]; }
            TestRunner.Equal(l3, exy - ex * ey, 1e-9, "Cov(home,away)");
        });

        r.Add("bivariate shared component never breaks a marginal", () =>
        {
            foreach (var (lh, la) in new[] { (0.05, 0.05), (0.4, 3.0), (6.0, 0.05) })
                foreach (var c in new[] { 0.0, 0.3, 0.9, 5.0 })
                {
                    var l3 = BivariatePoissonModel.SharedComponent(lh, la, BivariateMode.Proportional, c);
                    TestRunner.True(l3 >= 0, "shared component is non-negative");
                    TestRunner.True(lh - l3 > 0 && la - l3 > 0, $"both marginals stay positive (l3={l3})");
                    var l3c = BivariatePoissonModel.SharedComponent(lh, la, BivariateMode.Constant, c);
                    TestRunner.True(lh - l3c > 0 && la - l3c > 0, $"constant mode stays positive (l3={l3c})");
                }
        });

        r.Add("every model returns probabilities that sum to exactly 1", () =>
        {
            var cfg = Dc(rho: -0.05);
            foreach (var (lh, la) in new[] { (0.05, 0.05), (1.5, 1.2), (6.0, 6.0), (0.2, 4.4) })
            {
                var ip = DixonColesModel.Outcome1X2(lh, la, Dc());
                var dcp = DixonColesModel.Outcome1X2(lh, la, cfg);
                var bp = BivariatePoissonModel.Outcome1X2(lh, la, 0.3 * Math.Min(lh, la), cfg);
                foreach (var p in new[] { ip, dcp, bp })
                    TestRunner.Equal(1.0, p.Sum, 1e-12, "probability sum");
            }
        });

        // ---------------------------------------------------------------- the split itself

        r.Add("the split is a pure function of the date and covers every day", () =>
        {
            var s = Split();
            TestRunner.Equal(Segment.Train, s.Of(DateOnly.Parse("2017-06-27")), "first dataset day is train");
            TestRunner.Equal(Segment.Train, s.Of(s.ValidationStartDate.AddDays(-1)), "day before the boundary is train");
            TestRunner.Equal(Segment.Validation, s.Of(s.ValidationStartDate), "boundary day is validation");
            TestRunner.Equal(Segment.Validation, s.Of(s.TestStartDate.AddDays(-1)), "day before the test boundary is validation");
            TestRunner.Equal(Segment.Test, s.Of(s.TestStartDate), "test boundary day is test");
            TestRunner.True(!s.IsSelectable(s.TestStartDate), "the test boundary is not selectable");
            TestRunner.True(s.IsSelectable(s.TestStartDate.AddDays(-1)), "the day before it is");
        });

        r.Add("the test fence throws when a search touches a test match", () =>
        {
            var s = Split();
            var fence = new TestFence(s);
            fence.RecordScored("ok", s.TestStartDate.AddDays(-1));
            var threw = false;
            try { fence.RecordScored("bad", s.TestStartDate); }
            catch (InvalidOperationException) { threw = true; }
            TestRunner.True(threw, "a test-dated match must throw");
            TestRunner.Equal(1, fence.Breaches, "the breach is counted");
        });

        r.Add("the test fence removes and counts every test match handed to a search", () =>
        {
            var s = Split();
            var fence = new TestFence(s);
            var all = new List<MatchRecord>
            {
                M("a", "2021-01-01", "H", "A", 1, 0),
                M("b", "2023-01-01", "H", "A", 1, 0),
                M("c", "2025-01-01", "H", "A", 1, 0),
                M("d", "2026-01-01", "H", "A", 1, 0)
            };
            var kept = fence.Selectable(all);
            TestRunner.Equal(2, kept.Count, "only train and validation survive");
            TestRunner.Equal(2L, fence.MatchesWithheld, "both test matches are counted as withheld");
            TestRunner.True(kept.All(m => m.Date < s.TestStartDate), "nothing at or past the boundary survives");
        });

        // ---------------------------------------------------------------- leakage, on synthetic data

        r.Add("a later match cannot change an earlier prediction", () =>
        {
            var s = Split();
            var cfg = Dc(-0.05);
            var baseline = new List<MatchRecord>
            {
                M("m1", "2018-01-01", "T1", "T2", 2, 0), M("m2", "2018-01-08", "T2", "T1", 1, 1),
                M("m3", "2018-01-15", "T1", "T2", 0, 3), M("m4", "2018-01-22", "T2", "T1", 2, 2),
                M("m5", "2018-02-01", "T1", "T2", 1, 0)
            };
            var altered = baseline.Select(m => m.MatchId != "m5" ? m
                : M("m5", "2018-02-01", "T1", "T2", 7, 7)).ToList();

            List<MatchPrediction> Run(List<MatchRecord> ms)
            {
                var rows = StrengthPipeline.BuildRows(new TeamStrengthConfig(), ms);
                var outp = new List<MatchPrediction>();
                new BacktestV2(cfg, new ModelParameters(), s).Run(ms, rows, ModelMask.All, outp.Add);
                return outp;
            }

            var a = Run(baseline).Where(p => p.MatchId != "m5").ToList();
            var b = Run(altered).Where(p => p.MatchId != "m5").ToList();
            TestRunner.Equal(a.Count, b.Count, "same number of earlier predictions");
            for (var i = 0; i < a.Count; i++)
                foreach (var mid in ModelIds.All)
                {
                    TestRunner.Equal(a[i].Probabilities[(int)mid].Home, b[i].Probabilities[(int)mid].Home, 0.0, $"{mid} home, {a[i].MatchId}");
                    TestRunner.Equal(a[i].Probabilities[(int)mid].Draw, b[i].Probabilities[(int)mid].Draw, 0.0, $"{mid} draw, {a[i].MatchId}");
                }
        });

        r.Add("two matches on the same day cannot inform each other", () =>
        {
            var s = Split();
            var cfg = Dc();
            var ms = new List<MatchRecord>
            {
                M("w1", "2018-01-01", "T1", "T2", 1, 0), M("w2", "2018-01-08", "T3", "T4", 1, 0),
                M("s1", "2018-02-01", "T1", "T3", 0, 0), M("s2", "2018-02-01", "T2", "T4", 5, 0)
            };
            var altered = ms.Select(m => m.MatchId != "s2" ? m : M("s2", "2018-02-01", "T2", "T4", 0, 5)).ToList();

            List<MatchPrediction> Run(List<MatchRecord> list)
            {
                var rows = StrengthPipeline.BuildRows(new TeamStrengthConfig(), list);
                var outp = new List<MatchPrediction>();
                new BacktestV2(cfg, new ModelParameters(), s).Run(list, rows, ModelMask.All, outp.Add);
                return outp;
            }

            var p1 = Run(ms).First(p => p.MatchId == "s1");
            var p2 = Run(altered).First(p => p.MatchId == "s1");
            TestRunner.Equal(p1.LambdaHome, p2.LambdaHome, 0.0, "lambda home unchanged by the other match that day");
            TestRunner.Equal(p1.Probabilities[(int)ModelId.IndependentPoisson].Home,
                             p2.Probabilities[(int)ModelId.IndependentPoisson].Home, 0.0, "prediction unchanged");
        });

        r.Add("no snapshot is fed by a match on or after the match it predicts", () =>
        {
            var s = Split();
            var ms = new List<MatchRecord>();
            for (var i = 0; i < 40; i++)
                ms.Add(M($"m{i}", DateOnly.Parse("2018-01-01").AddDays(i * 3).ToString("yyyy-MM-dd"),
                    i % 2 == 0 ? "T1" : "T3", i % 2 == 0 ? "T2" : "T4", i % 4, (i + 1) % 3));
            var rows = StrengthPipeline.BuildRows(new TeamStrengthConfig(), ms);
            var outp = new List<MatchPrediction>();
            new BacktestV2(Dc(), new ModelParameters(), s).Run(ms, rows, ModelMask.All, outp.Add);
            TestRunner.Equal(0, outp.Count(p => p.EvidenceCutoff.HasValue && p.EvidenceCutoff.Value >= p.Date),
                "predictions built on evidence dated at or after the match");
        });

        // ---------------------------------------------------------------- what is NOT a parameter

        r.Add("cold-start thresholds are labels: changing them cannot change a probability", () =>
        {
            var s = Split();
            var ms = Enumerable.Range(0, 30).Select(i =>
                M($"m{i}", DateOnly.Parse("2018-01-01").AddDays(i * 5).ToString("yyyy-MM-dd"),
                    i % 3 == 0 ? "T1" : "T3", i % 3 == 0 ? "T2" : "T4", i % 4, (i + 2) % 3)).ToList();

            List<MatchPrediction> Run(TeamStrengthConfig cfg)
            {
                var rows = StrengthPipeline.BuildRows(cfg, ms);
                var outp = new List<MatchPrediction>();
                new BacktestV2(Dc(-0.05), new ModelParameters(), s).Run(ms, rows, ModelMask.All, outp.Add);
                return outp;
            }

            var baseCfg = new TeamStrengthConfig();
            var altered = StrengthPipeline.Copy(baseCfg);
            altered.LimitedThreshold = 2; altered.DevelopingThreshold = 8;
            altered.EstablishedThreshold = 15; altered.RichThreshold = 40;

            var a = Run(baseCfg); var b = Run(altered);
            for (var i = 0; i < a.Count; i++)
                foreach (var mid in ModelIds.All)
                    TestRunner.Equal(a[i].Probabilities[(int)mid].Home, b[i].Probabilities[(int)mid].Home, 0.0,
                        $"{mid} home probability of {a[i].MatchId}");
            TestRunner.True(a.Zip(b).Any(z => z.First.WeakestColdStartClass != z.Second.WeakestColdStartClass),
                "the labels themselves did change - otherwise the test proves nothing");
        });

        r.Add("venue shrinkage is not read by any 1X2 model", () =>
        {
            var s = Split();
            var ms = Enumerable.Range(0, 30).Select(i =>
                M($"m{i}", DateOnly.Parse("2018-01-01").AddDays(i * 5).ToString("yyyy-MM-dd"),
                    i % 2 == 0 ? "T1" : "T2", i % 2 == 0 ? "T2" : "T1", i % 4, (i + 2) % 3)).ToList();

            double Loss(double venueK)
            {
                var cfg = new TeamStrengthConfig { VenueShrinkageK = venueK };
                var rows = StrengthPipeline.BuildRows(cfg, ms);
                var acc = new MetricAccumulator("X", "s", "g");
                new BacktestV2(Dc(-0.05), new ModelParameters(), s).Run(ms, rows, ModelMask.All,
                    p => acc.Add(p.Probabilities[(int)ModelId.IndependentPoisson], p.Actual));
                return acc.LogLoss;
            }

            TestRunner.Equal(Loss(1.0), Loss(16.0), 0.0, "venue shrinkage changed the log loss");
        });

        // ---------------------------------------------------------------- determinism

        r.Add("two identical runs produce identical numbers", () =>
        {
            var s = Split();
            var ms = Enumerable.Range(0, 40).Select(i =>
                M($"m{i}", DateOnly.Parse("2018-01-01").AddDays(i * 4).ToString("yyyy-MM-dd"),
                    i % 3 == 0 ? "T1" : "T3", i % 3 == 0 ? "T2" : "T4", i % 4, (i + 1) % 3)).ToList();

            double Loss()
            {
                var rows = StrengthPipeline.BuildRows(new TeamStrengthConfig(), ms);
                var acc = new MetricAccumulator("X", "s", "g");
                new BacktestV2(Dc(-0.05), new ModelParameters { BivariateC = 0.2 }, s)
                    .Run(ms, rows, ModelMask.All, p => acc.Add(p.Probabilities[(int)ModelId.BivariatePoisson], p.Actual));
                return acc.LogLoss;
            }
            TestRunner.Equal(Loss(), Loss(), 0.0, "the harness is not deterministic");
        });

        r.Add("the paired bootstrap reports no difference between a model and itself", () =>
        {
            var s = Split();
            var ms = Enumerable.Range(0, 60).Select(i =>
                M($"m{i}", DateOnly.Parse("2018-01-01").AddDays(i * 3).ToString("yyyy-MM-dd"),
                    i % 2 == 0 ? "T1" : "T3", i % 2 == 0 ? "T2" : "T4", i % 4, (i + 1) % 3)).ToList();
            var rows = StrengthPipeline.BuildRows(new TeamStrengthConfig(), ms);
            var preds = new List<MatchPrediction>();
            new BacktestV2(Dc(), new ModelParameters { BivariateC = 0.0 }, s).Run(ms, rows, ModelMask.All, preds.Add);

            // bivariate with c = 0 IS independent Poisson, so the interval must contain nothing but zero
            var cmp = PairedBootstrap.Compare(preds, ModelId.BivariatePoisson, ModelId.IndependentPoisson,
                "TEST", "self", resamples: 200);
            TestRunner.Equal(0.0, cmp.DeltaLogLoss, 1e-12, "delta");
            TestRunner.Equal(0.0, cmp.CiLow, 1e-12, "lower bound");
            TestRunner.Equal(0.0, cmp.CiHigh, 1e-12, "upper bound");
            TestRunner.True(!cmp.CiExcludesZero, "an identical model must not be called significantly better");
            TestRunner.True(cmp.Verdict.StartsWith("IDENTICAL", StringComparison.Ordinal),
                $"a last-bit disagreement must be reported as identical, not as a win (got '{cmp.Verdict}')");
        });

        // ---------------------------------------------------------------- real data

        if (datasetPath is null || snapshotPath is null) return;

        r.Add("real data: the V2 harness reproduces the V1 backtest exactly", () =>
        {
            var hygiene = tsConfigPath is not null ? TeamStrengthConfig.Load(tsConfigPath) : new TeamStrengthConfig();
            var matches = Formax.TeamStrength.Services.MatchCsvReader.Read(datasetPath, hygiene).Matches;
            var snapshots = StrengthSnapshotReader.Read(snapshotPath);
            var cfg = dcConfigPath is not null ? DixonColesConfig.Load(dcConfigPath) : new DixonColesConfig();
            var s = splitPath is not null ? SplitConfig.Load(splitPath) : Split();

            var accs = ModelIds.All.ToDictionary(m => m, m => new MetricAccumulator(ModelIds.Name(m), "FULL", "all"));
            new BacktestV2(cfg, new ModelParameters { Rho = cfg.Rho }, s)
                .Run(matches, snapshots, ModelMask.All, p =>
                {
                    foreach (var m in ModelIds.All) accs[m].Add(p.Probabilities[(int)m], p.Actual);
                });

            // the four numbers published by the V1 backtest, on the same 33,901 predictions
            TestRunner.Equal(33901, accs[ModelId.Simple].N, "prediction count");
            TestRunner.Equal(1.06727, accs[ModelId.Simple].LogLoss, 5e-6, "SIMPLE log loss");
            TestRunner.Equal(1.02587, accs[ModelId.TeamStrength].LogLoss, 5e-6, "TEAM_STRENGTH log loss");
            TestRunner.Equal(1.01318, accs[ModelId.IndependentPoisson].LogLoss, 5e-6, "INDEPENDENT_POISSON log loss");
            TestRunner.Equal(1.01515, accs[ModelId.DixonColes].LogLoss, 5e-6, "DIXON_COLES log loss");
        });

        r.Add("real data: rebuilding strength in memory matches the published CSV snapshots", () =>
        {
            var hygiene = tsConfigPath is not null ? TeamStrengthConfig.Load(tsConfigPath) : new TeamStrengthConfig();
            var matches = Formax.TeamStrength.Services.MatchCsvReader.Read(datasetPath, hygiene).Matches;
            var fromCsv = StrengthSnapshotReader.Read(snapshotPath);
            var inMemory = StrengthPipeline.BuildRows(hygiene, matches);

            TestRunner.Equal(fromCsv.Count, inMemory.Count, "snapshot count");
            var maxDiff = 0.0;
            foreach (var kv in fromCsv)
            {
                var m = inMemory[kv.Key];
                maxDiff = Math.Max(maxDiff, Math.Abs(kv.Value.Attack - m.Attack));
                maxDiff = Math.Max(maxDiff, Math.Abs(kv.Value.Defense - m.Defense));
            }
            // the CSV rounds every index to six decimals, so half a unit in the last place is the bound
            TestRunner.True(maxDiff <= 5e-7, $"in-memory and CSV strengths differ by {maxDiff:0.0e+0}, above CSV rounding");
        });

        r.Add("real data: the split lands in a calendar gap, not inside a matchday", () =>
        {
            var hygiene = tsConfigPath is not null ? TeamStrengthConfig.Load(tsConfigPath) : new TeamStrengthConfig();
            var matches = Formax.TeamStrength.Services.MatchCsvReader.Read(datasetPath, hygiene).Matches;
            var s = splitPath is not null ? SplitConfig.Load(splitPath) : Split();

            foreach (var boundary in new[] { s.ValidationStartDate, s.TestStartDate })
            {
                TestRunner.Equal(0, matches.Count(m => m.Date == boundary), $"matches exactly on {boundary:yyyy-MM-dd}");
                var before = matches.Where(m => m.Date < boundary).Max(m => m.Date);
                var after = matches.Where(m => m.Date >= boundary).Min(m => m.Date);
                TestRunner.True((after.DayNumber - before.DayNumber) >= 7,
                    $"boundary {boundary:yyyy-MM-dd} sits in a gap of only {after.DayNumber - before.DayNumber} days");
            }

            foreach (var seg in new[] { Segment.Train, Segment.Validation, Segment.Test })
                TestRunner.True(matches.Count(m => s.Of(m.Date) == seg) > 5000, $"{seg} is large enough to measure");
        });

        r.Add("real data: fencing the test segment does not change a single pre-test prediction", () =>
        {
            var hygiene = tsConfigPath is not null ? TeamStrengthConfig.Load(tsConfigPath) : new TeamStrengthConfig();
            var matches = Formax.TeamStrength.Services.MatchCsvReader.Read(datasetPath, hygiene).Matches;
            var cfg = dcConfigPath is not null ? DixonColesConfig.Load(dcConfigPath) : new DixonColesConfig();
            var s = splitPath is not null ? SplitConfig.Load(splitPath) : Split();

            var fenced = matches.Where(m => m.Date < s.TestStartDate).ToList();

            List<MatchPrediction> Run(IReadOnlyList<MatchRecord> ms)
            {
                var rows = StrengthPipeline.BuildRows(hygiene, ms);
                var outp = new List<MatchPrediction>();
                new BacktestV2(cfg, new ModelParameters { Rho = cfg.Rho }, s)
                    .Run(ms, rows, ModelMask.IndependentPoisson, outp.Add);
                return outp;
            }

            var full = Run(matches).Where(p => p.Segment != Segment.Test).ToList();
            var part = Run(fenced);
            TestRunner.Equal(full.Count, part.Count, "same number of pre-test predictions");
            var drift = 0.0;
            for (var i = 0; i < full.Count; i++)
                drift = Math.Max(drift, Math.Abs(full[i].Probabilities[(int)ModelId.IndependentPoisson].Home
                                               - part[i].Probabilities[(int)ModelId.IndependentPoisson].Home));
            TestRunner.Equal(0.0, drift, 0.0, "removing the test segment changed an earlier prediction");
        });
    }
}
