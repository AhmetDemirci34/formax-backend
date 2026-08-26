using Formax.Calibration.Calibrators;
using Formax.Calibration.Models;
using Formax.Calibration.Services;
using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.ModelValidation.Services;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Tests;

namespace Formax.Calibration.Tests;

public static class CalibrationTests
{
    private static ProbTriple P(double h, double d, double a) => new(h, d, a, Numerics.ProbabilityFloor);

    /// <summary>Deterministic synthetic history: probabilities are stated, outcomes follow a fixed pattern.</summary>
    private static List<CalibrationSample> Synthetic(int n, Func<int, ProbTriple> prob, Func<int, ProbTriple, Outcome> outcome,
        DateOnly? start = null)
    {
        var d0 = start ?? DateOnly.Parse("2018-01-01");
        var list = new List<CalibrationSample>(n);
        for (var i = 0; i < n; i++)
        {
            var p = prob(i);
            list.Add(new CalibrationSample(d0.AddDays(i / 8), p, outcome(i, p), Segment.Train));
        }
        return list;
    }

    private static double MeanNll(ICalibrator c, IReadOnlyList<CalibrationSample> data)
    {
        var sum = 0.0;
        foreach (var s in data) sum += -Math.Log(Math.Max(c.Apply(s.Raw)[s.Actual], 1e-15));
        return sum / data.Count;
    }

    private static double RawNll(IReadOnlyList<CalibrationSample> data)
        => MeanNll(new NoCalibration(), data);

    public static void Register(TestRunner r, string? datasetPath, string? splitPath,
        string? dcConfigPath, string? validatedTsPath)
    {
        // ---------------------------------------------------------------- the simplex guarantee

        r.Add("every calibrator returns a probability distribution, for every input", () =>
        {
            var history = Synthetic(1200,
                i => P(0.30 + 0.0004 * (i % 900), 0.25, 0.45 - 0.0004 * (i % 900)),
                (i, p) => (Outcome)(i % 3));

            var calibrators = new ICalibrator[]
            {
                new TemperatureScaling(), new MatrixScaling(true), new MatrixScaling(false), new IsotonicOvr()
            };
            foreach (var c in calibrators)
            {
                c.Fit(history);
                foreach (var raw in new[]
                {
                    P(1.0 / 3, 1.0 / 3, 1.0 / 3), P(0.999, 0.0005, 0.0005), P(1e-9, 1e-9, 1.0),
                    P(0.5, 0.5, 1e-12), P(0.02, 0.96, 0.02)
                })
                {
                    var q = c.Apply(raw);
                    TestRunner.Equal(1.0, q.Sum, 1e-12, $"{c.Name} probability sum");
                    TestRunner.True(q.Home >= 0 && q.Home <= 1 && q.Draw >= 0 && q.Draw <= 1 && q.Away >= 0 && q.Away <= 1,
                        $"{c.Name} produced a probability outside [0,1]");
                }
            }
        });

        // ---------------------------------------------------------------- the identity start

        r.Add("a calibrator fitted on already-calibrated data stays close to the identity", () =>
        {
            // outcomes follow the stated probabilities exactly: 45% home, 25% draw, 30% away
            var history = Synthetic(2000,
                _ => P(0.45, 0.25, 0.30),
                (i, _) => (i % 20) < 9 ? Outcome.HomeWin : (i % 20) < 14 ? Outcome.Draw : Outcome.AwayWin);

            var t = new TemperatureScaling();
            t.Fit(history);
            TestRunner.Equal(1.0, t.Temperature, 0.05, "temperature on already-calibrated data");

            var q = t.Apply(P(0.45, 0.25, 0.30));
            TestRunner.Equal(0.45, q.Home, 0.02, "home probability barely moves");
        });

        r.Add("temperature sharpens an under-confident model and flattens an over-confident one", () =>
        {
            // says 50% home but home wins 75% of the time -> the model is under-confident -> T < 1
            var under = Synthetic(2000,
                _ => P(0.50, 0.25, 0.25),
                (i, _) => (i % 4) < 3 ? Outcome.HomeWin : (i % 4) == 3 ? Outcome.AwayWin : Outcome.Draw);
            var tu = new TemperatureScaling();
            tu.Fit(under);
            TestRunner.True(tu.Temperature < 1.0, $"under-confident model should sharpen, got T={tu.Temperature:0.000}");
            TestRunner.True(tu.Apply(P(0.50, 0.25, 0.25)).Home > 0.50, "the home probability should rise");

            // says 90% home but home wins only 45% of the time -> over-confident -> T > 1
            var over = Synthetic(2000,
                _ => P(0.90, 0.05, 0.05),
                (i, _) => (i % 20) < 9 ? Outcome.HomeWin : (i % 20) < 14 ? Outcome.Draw : Outcome.AwayWin);
            var to = new TemperatureScaling();
            to.Fit(over);
            TestRunner.True(to.Temperature > 1.0, $"over-confident model should flatten, got T={to.Temperature:0.000}");
            TestRunner.True(to.Apply(P(0.90, 0.05, 0.05)).Home < 0.90, "the home probability should fall");
        });

        // ---------------------------------------------------------------- the nesting

        r.Add("the three scaling methods are nested: matrix <= vector <= temperature <= raw, in sample", () =>
        {
            var history = Synthetic(3000,
                i => P(0.25 + 0.0002 * (i % 2000), 0.28, 0.47 - 0.0002 * (i % 2000)),
                (i, p) => p.Home > 0.45 ? (i % 5 == 0 ? Outcome.Draw : Outcome.HomeWin)
                        : (i % 3 == 0 ? Outcome.AwayWin : i % 3 == 1 ? Outcome.Draw : Outcome.HomeWin));

            var raw = RawNll(history);
            var t = new TemperatureScaling(); t.Fit(history);
            var v = new MatrixScaling(true); v.Fit(history);
            var m = new MatrixScaling(false); m.Fit(history);

            var nT = MeanNll(t, history);
            var nV = MeanNll(v, history);
            var nM = MeanNll(m, history);

            TestRunner.True(nT <= raw + 1e-9, $"temperature must not be worse than raw in sample ({nT:0.000000} vs {raw:0.000000})");
            TestRunner.True(nV <= nT + 1e-6, $"vector must not be worse than temperature in sample ({nV:0.000000} vs {nT:0.000000})");
            TestRunner.True(nM <= nV + 1e-6, $"matrix must not be worse than vector in sample ({nM:0.000000} vs {nV:0.000000})");
        });

        r.Add("the optimiser converges: final gradient is flat", () =>
        {
            var history = Synthetic(2500,
                i => P(0.30 + 0.0003 * (i % 1500), 0.26, 0.44 - 0.0003 * (i % 1500)),
                (i, _) => (Outcome)((i * 7) % 3));
            var m = new MatrixScaling(false);
            m.Fit(history);
            TestRunner.True(m.Report is not null, "no fit report");
            TestRunner.True(m.Report!.Converged, $"multinomial fit did not converge: |grad|={m.Report.FinalGradientNorm:0.0e+0} after {m.Report.Iterations} iterations");
        });

        // ---------------------------------------------------------------- isotonic

        r.Add("PAVA produces a non-decreasing fit", () =>
        {
            var x = new[] { 0.1, 0.2, 0.3, 0.4, 0.5 };
            var y = new[] { 1.0, 0.0, 1.0, 0.0, 1.0 };
            var (lo, hi, v) = Numerics.Pava(x, y);
            for (var i = 1; i < v.Length; i++)
                TestRunner.True(v[i] >= v[i - 1] - 1e-12, "isotonic values must be non-decreasing");
            for (var i = 0; i < v.Length; i++)
                TestRunner.True(v[i] >= 0 && v[i] <= 1, "isotonic values must be probabilities");
            TestRunner.True(lo[0] <= hi[^1], "blocks must span the input range");
        });

        r.Add("isotonic one-vs-rest moves probabilities towards the observed frequency", () =>
        {
            // model always says 40% home, home actually wins 70% of the time
            var history = Synthetic(2000,
                _ => P(0.40, 0.30, 0.30),
                (i, _) => (i % 10) < 7 ? Outcome.HomeWin : (i % 10) < 8 ? Outcome.Draw : Outcome.AwayWin);
            var iso = new IsotonicOvr();
            iso.Fit(history);
            var q = iso.Apply(P(0.40, 0.30, 0.30));
            TestRunner.True(q.Home > 0.55, $"isotonic should pull 0.40 up towards 0.70, got {q.Home:0.000}");
            TestRunner.Equal(1.0, q.Sum, 1e-12, "still a distribution after renormalisation");
        });

        // ---------------------------------------------------------------- calibration error

        r.Add("calibration error is zero for a perfectly calibrated set and positive otherwise", () =>
        {
            var perfect = new List<(double p, double y)>();
            for (var i = 0; i < 1000; i++) perfect.Add((0.75, i % 4 == 0 ? 0.0 : 1.0));   // 75% claimed, 75% observed
            var (ece, _) = CalibrationMetrics.Error(perfect);
            TestRunner.Equal(0.0, ece, 1e-9, "perfectly calibrated set");

            var wrong = new List<(double p, double y)>();
            for (var i = 0; i < 1000; i++) wrong.Add((0.75, i % 2 == 0 ? 0.0 : 1.0));     // 75% claimed, 50% observed
            var (ece2, mce2) = CalibrationMetrics.Error(wrong);
            TestRunner.Equal(0.25, ece2, 1e-9, "expected calibration error");
            TestRunner.Equal(0.25, mce2, 1e-9, "max calibration error");
        });

        r.Add("reliability bands report the sample size, the claim and the outcome", () =>
        {
            var pairs = new List<(double p, double y)>();
            for (var i = 0; i < 100; i++) pairs.Add((0.65, i < 72 ? 1.0 : 0.0));
            var rows = CalibrationMetrics.Bands(pairs, "X", "TEST", "ALL_CLASSES");
            var band = rows.First(b => b.Band == "60-70%");
            TestRunner.Equal(100, band.N, "sample size");
            TestRunner.Equal(0.65, band.PredictedMean, 1e-12, "predicted mean");
            TestRunner.Equal(0.72, band.ActualFrequency, 1e-12, "actual frequency");
            TestRunner.Equal(-0.07, band.Difference, 1e-12, "difference");
        });

        // ---------------------------------------------------------------- temporal safety

        r.Add("the expanding regime cannot see the future: later outcomes do not move earlier probabilities", () =>
        {
            var split = splitPath is not null ? SplitConfig.Load(splitPath) : SplitConfig.Load("(none)");
            var baseline = Synthetic(4000,
                i => P(0.30 + 0.0002 * (i % 1500), 0.27, 0.43 - 0.0002 * (i % 1500)),
                (i, _) => (Outcome)((i * 5) % 3), DateOnly.Parse("2018-01-01"));

            var cutoff = baseline[^600].Date;
            var altered = baseline.Select(s => s.Date < cutoff ? s
                : new CalibrationSample(s.Date, s.Raw, Outcome.Draw, s.Segment)).ToList();

            var a = CalibrationRunner.Run(new TemperatureScaling(), FitRegime.Expanding, baseline, split);
            var b = CalibrationRunner.Run(new TemperatureScaling(), FitRegime.Expanding, altered, split);

            var drift = 0.0;
            for (var i = 0; i < baseline.Count; i++)
                if (baseline[i].Date < cutoff)
                    drift = Math.Max(drift, Math.Abs(a.Calibrated[i].Home - b.Calibrated[i].Home));
            TestRunner.Equal(0.0, drift, 0.0, "changing later results changed an earlier calibrated probability");

            // and the change must actually have done something later, or the test proves nothing
            var laterDrift = 0.0;
            for (var i = 0; i < baseline.Count; i++)
                if (baseline[i].Date >= cutoff)
                    laterDrift = Math.Max(laterDrift, Math.Abs(a.Calibrated[i].Home - b.Calibrated[i].Home));
            TestRunner.True(laterDrift > 0, "the perturbation had no effect at all - the test is vacuous");
        });

        r.Add("the expanding regime does not calibrate before it has enough history", () =>
        {
            var split = SplitConfig.Load("(none)");
            var data = Synthetic(300, _ => P(0.50, 0.25, 0.25), (i, _) => (Outcome)(i % 3));
            var run = CalibrationRunner.Run(new TemperatureScaling(), FitRegime.Expanding, data, split);
            for (var i = 0; i < data.Count; i++)
                TestRunner.Equal(data[i].Raw.Home, run.Calibrated[i].Home, 0.0,
                    "with less history than the minimum the raw probability must pass through untouched");
            TestRunner.Equal(0, run.RefitTrace.Count(t => t.Active), "no refit should have been active");
        });

        r.Add("the static regime is fitted on TRAIN only", () =>
        {
            var split = splitPath is not null ? SplitConfig.Load(splitPath) : SplitConfig.Load("(none)");
            var data = new List<CalibrationSample>();
            for (var i = 0; i < 3000; i++)
            {
                var date = DateOnly.Parse("2018-01-01").AddDays(i);
                data.Add(new CalibrationSample(date, P(0.45, 0.25, 0.30), (Outcome)(i % 3), split.Of(date)));
            }
            var run = CalibrationRunner.Run(new TemperatureScaling(), FitRegime.StaticTrain, data, split);
            TestRunner.True(run.LatestTrainingDate!.Value < split.ValidationStartDate,
                $"static fit reached {run.LatestTrainingDate:yyyy-MM-dd}, at or past the validation boundary");
            TestRunner.Equal(1, run.RefitTrace.Count, "the static regime must fit exactly once");
        });

        r.Add("a calibrator sees only the raw probability - not the date, the team or the competition", () =>
        {
            var history = Synthetic(1500,
                i => P(0.30 + 0.0002 * (i % 1000), 0.27, 0.43 - 0.0002 * (i % 1000)),
                (i, _) => (Outcome)((i * 3) % 3));
            foreach (var c in new ICalibrator[] { new TemperatureScaling(), new MatrixScaling(true), new MatrixScaling(false), new IsotonicOvr() })
            {
                c.Fit(history);
                var raw = P(0.52, 0.24, 0.24);
                var first = c.Apply(raw);
                var second = c.Apply(raw);
                TestRunner.Equal(first.Home, second.Home, 0.0, $"{c.Name} is not a pure function of the raw probability");
                TestRunner.Equal(first.Draw, second.Draw, 0.0, $"{c.Name} is not a pure function of the raw probability");
            }
        });

        r.Add("two identical fits produce identical parameters", () =>
        {
            var history = Synthetic(2000,
                i => P(0.28 + 0.0003 * (i % 1200), 0.29, 0.43 - 0.0003 * (i % 1200)),
                (i, _) => (Outcome)((i * 11) % 3));
            var a = new MatrixScaling(false); a.Fit(history);
            var b = new MatrixScaling(false); b.Fit(history);
            TestRunner.Equal(a.ParametersJson(), b.ParametersJson(), "the fit is not deterministic");
        });

        // ---------------------------------------------------------------- real data

        if (datasetPath is null || validatedTsPath is null || dcConfigPath is null) return;

        r.Add("real data: the frozen base model still produces the validated V2 probabilities", () =>
        {
            var ts = TeamStrengthConfig.Load(validatedTsPath);
            var dc = DixonColesConfig.Load(dcConfigPath);
            var split = splitPath is not null ? SplitConfig.Load(splitPath) : SplitConfig.Load("(none)");
            var matches = Formax.TeamStrength.Services.MatchCsvReader.Read(datasetPath, ts).Matches;

            var preds = new List<MatchPrediction>();
            new BacktestV2(dc, new ModelParameters { Rho = 0.0, BivariateC = 0.0 }, split)
                .Run(matches, StrengthPipeline.BuildRows(ts, matches), ModelMask.IndependentPoisson, preds.Add);

            double Ll(Segment seg)
            {
                var sum = 0.0; var n = 0;
                foreach (var p in preds.Where(p => p.Segment == seg))
                {
                    sum += -Math.Log(Math.Max(p.Probabilities[(int)ModelId.IndependentPoisson][p.Actual], 1e-15));
                    n++;
                }
                return sum / n;
            }

            TestRunner.Equal(33901, preds.Count, "prediction count");
            TestRunner.Equal(0.987473, Ll(Segment.Validation), 5e-6, "validation log loss of the frozen model");
            TestRunner.Equal(0.993544, Ll(Segment.Test), 5e-6, "test log loss of the frozen model");
        });

        r.Add("real data: the validated config is read unchanged from model_validation_v2", () =>
        {
            var ts = TeamStrengthConfig.Load(validatedTsPath);
            TestRunner.Equal(100000.0, ts.HalfLifeDays, 0.0, "halfLifeDays");
            TestRunner.Equal(0.08, ts.LearningRate, 1e-12, "learningRate");
            TestRunner.Equal(0.0, ts.ShrinkageK, 0.0, "shrinkageK");
            TestRunner.Equal(1.5, ts.RatioSmoothing, 1e-12, "ratioSmoothing");
            TestRunner.Equal(false, ts.UseCompetitionTypePool, "useCompetitionTypePool");
        });
    }
}
