using System.Globalization;
using System.Text;
using Formax.Calibration.Calibrators;
using Formax.Calibration.Models;
using Formax.Calibration.Services;
using Formax.Calibration.Tests;
using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.ModelValidation.Services;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;
using Formax.TeamStrength.Tests;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var root = FindRepoRoot(AppContext.BaseDirectory);
var datasetPath = ArgValue("--dataset") ?? Path.Combine(root, "FORMAX_HISTORICAL_MASTER", "FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv");
var splitPath = ArgValue("--split") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "model_validation_v2", "split.config.json");
var dcConfigPath = ArgValue("--dc-config") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "dixon_coles", "dixoncoles.config.json");
var validatedTsPath = ArgValue("--ts-config") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "model_validation_v2", "validated_teamstrength_config.json");
var outDir = ArgValue("--out") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "calibration_v1");

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
switch (command)
{
    case "test": return RunTests();
    case "run": return RunAll();
    case "all": { var t = RunTests(); return t != 0 ? t : RunAll(); }
    default:
        Console.WriteLine("FormaxCalibration - probability calibration for the frozen Independent Poisson V2 baseline (research only)");
        Console.WriteLine("  test   calibrator mathematics, simplex guarantees and temporal-leakage tests");
        Console.WriteLine("  run    fit on TRAIN / choose on VALIDATION / score once on TEST");
        Console.WriteLine("  all    test, then run");
        return 0;
}

string? ArgValue(string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
    return null;
}

static string FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "FORMAX_HISTORICAL_MASTER"))) return dir.FullName;
        dir = dir.Parent;
    }
    return Directory.GetCurrentDirectory();
}

int RunTests()
{
    var runner = new TestRunner();
    CalibrationTests.Register(runner,
        File.Exists(datasetPath) ? datasetPath : null,
        File.Exists(splitPath) ? splitPath : null,
        File.Exists(dcConfigPath) ? dcConfigPath : null,
        File.Exists(validatedTsPath) ? validatedTsPath : null);
    if (!File.Exists(datasetPath)) Console.WriteLine("  (real-data tests skipped: dataset not found)");
    return runner.Run();
}

int RunAll()
{
    var total = System.Diagnostics.Stopwatch.StartNew();
    Directory.CreateDirectory(outDir);

    // ---------------------------------------------------------------- the frozen model
    var ts = TeamStrengthConfig.Load(validatedTsPath);
    var dc = DixonColesConfig.Load(dcConfigPath);
    var split = SplitConfig.Load(splitPath);
    var matches = Formax.TeamStrength.Services.MatchCsvReader.Read(datasetPath, ts).Matches;

    Console.WriteLine("== FORMAX PROBABILITY CALIBRATION V1 ==");
    Console.WriteLine($"model     : INDEPENDENT_POISSON_V2 on validated team strength - FROZEN, not re-tuned");
    Console.WriteLine($"ts config : {validatedTsPath}");
    Console.WriteLine($"            {ts.Describe()}");
    Console.WriteLine($"split     : {split.Describe()}");
    Console.WriteLine($"matches   : {matches.Count}");

    var rawPreds = new List<MatchPrediction>(matches.Count);
    var report = new BacktestV2(dc, new ModelParameters { Rho = 0.0, BivariateC = 0.0 }, split)
        .Run(matches, StrengthPipeline.BuildRows(ts, matches), ModelMask.IndependentPoisson, rawPreds.Add);
    Console.WriteLine($"raw       : {report.MatchesPredicted} predictions, leakage {report.LeakageViolations}, normalisation {report.NormalisationViolations}");

    // the samples a calibrator may see: probability in, outcome out, nothing else - in date order
    var samples = rawPreds
        .Select(p => new CalibrationSample(p.Date, p.Probabilities[(int)ModelId.IndependentPoisson], p.Actual, p.Segment))
        .ToList();
    for (var i = 1; i < samples.Count; i++)
        if (samples[i].Date < samples[i - 1].Date) throw new InvalidOperationException("samples are not in date order");

    foreach (var g in samples.GroupBy(s => s.Segment).OrderBy(g => g.Key))
        Console.WriteLine($"            {g.Key,-10} {g.Count(),6} matches");
    Console.WriteLine();

    // ---------------------------------------------------------------- calibration runs
    var prototypes = new ICalibrator[]
    {
        new TemperatureScaling(),
        new MatrixScaling(diagonal: true),
        new MatrixScaling(diagonal: false),
        new IsotonicOvr()
    };

    var runs = new List<CalibrationRun>();
    foreach (var proto in prototypes)
        foreach (var regime in new[] { FitRegime.StaticTrain, FitRegime.Expanding })
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var run = CalibrationRunner.Run(proto, regime, samples, split);
            runs.Add(run);
            var active = run.RefitTrace.Count(r => r.Active);
            Console.WriteLine($"fitted {run.Key,-42} {sw.ElapsedMilliseconds,6} ms   refits {active}/{run.RefitTrace.Count}   {run.FinalModel.DescribeParameters()}");
        }
    Console.WriteLine();

    // every variant, including the raw model, as a uniform lookup
    var variants = new List<(string method, string regime, string key, Func<int, ProbTriple> probs, bool trainInSample)>
    {
        ("NO_CALIBRATION", "-", "NO_CALIBRATION", i => samples[i].Raw, false)
    };
    foreach (var r in runs)
        variants.Add((r.Method, r.Regime.ToString(), r.Key, i => r.Calibrated[i], r.Regime == FitRegime.StaticTrain));

    List<(ProbTriple, Outcome)> Slice(Func<int, ProbTriple> probs, Func<int, bool> keep)
    {
        var list = new List<(ProbTriple, Outcome)>();
        for (var i = 0; i < samples.Count; i++) if (keep(i)) list.Add((probs(i), samples[i].Actual));
        return list;
    }

    // ---------------------------------------------------------------- overall scores
    var scores = new List<Score>();
    foreach (var v in variants)
        foreach (var seg in new[] { Segment.Train, Segment.Validation, Segment.Test })
        {
            var data = Slice(v.probs, i => samples[i].Segment == seg);
            scores.Add(Score.Of(data, v.method, v.regime, seg.ToString().ToUpperInvariant(), "OVERALL", "all",
                inSample: v.trainInSample && seg == Segment.Train));
        }

    // ---------------------------------------------------------------- SELECTION - validation only
    Console.WriteLine("VALIDATION (7.636 matches) - the only numbers the choice is allowed to use");
    Console.WriteLine($"{"method",-42}{"LogLoss",11}{"Brier",10}{"RPS",10}{"CalErr",10}{"Accuracy",10}");
    var validationScores = scores.Where(s => s.Segment == "VALIDATION" && s.Scope == "OVERALL").ToList();
    foreach (var s in validationScores.OrderBy(s => s.LogLoss))
        Console.WriteLine($"{Key(s),-42}{s.LogLoss,11:0.000000}{s.Brier,10:0.00000}{s.Rps,10:0.00000}{s.CalibrationError,10:0.00000}{s.Accuracy,10:0.0000}");

    // priority: log loss, then Brier, then RPS, then calibration error - declared before the run
    var winner = validationScores
        .OrderBy(s => s.LogLoss).ThenBy(s => s.Brier).ThenBy(s => s.Rps).ThenBy(s => s.CalibrationError)
        .First();
    var rawValidation = validationScores.First(s => s.Method == "NO_CALIBRATION");
    var selectedKey = Key(winner);
    var selected = variants.First(v => v.key == selectedKey || (v.method == winner.Method && v.regime == winner.Regime));

    Console.WriteLine();
    Console.WriteLine($"SELECTED ON VALIDATION: {selectedKey}");
    Console.WriteLine($"  validation log loss {winner.LogLoss:0.000000} vs raw {rawValidation.LogLoss:0.000000} " +
                      $"({winner.LogLoss - rawValidation.LogLoss:+0.000000;-0.000000;0})");
    Console.WriteLine("  the test segment has not been scored yet - it is scored once, below, for this choice.");
    Console.WriteLine();

    // ---------------------------------------------------------------- TEST, after the choice is locked
    var testRaw = scores.First(s => s.Method == "NO_CALIBRATION" && s.Segment == "TEST");
    var testSel = scores.First(s => Key(s) == selectedKey && s.Segment == "TEST");

    Console.WriteLine("TEST (7.793 matches) - single evaluation of the locked choice");
    Console.WriteLine($"{"",-42}{"LogLoss",11}{"Brier",10}{"RPS",10}{"CalErr",10}{"Accuracy",10}");
    Console.WriteLine($"{"RAW (no calibration)",-42}{testRaw.LogLoss,11:0.000000}{testRaw.Brier,10:0.00000}{testRaw.Rps,10:0.00000}{testRaw.CalibrationError,10:0.00000}{testRaw.Accuracy,10:0.0000}");
    Console.WriteLine($"{selectedKey,-42}{testSel.LogLoss,11:0.000000}{testSel.Brier,10:0.00000}{testSel.Rps,10:0.00000}{testSel.CalibrationError,10:0.00000}{testSel.Accuracy,10:0.0000}");
    Console.WriteLine();

    // The other methods on TEST, computed AFTER the choice above was locked. They decided nothing;
    // they are here so the reader can see whether the outcome is specific to the selected method or
    // general - which is the difference between "we picked the wrong one" and "calibration does not
    // help this model".
    Console.WriteLine("all methods on TEST - reported after the choice was locked, used for nothing");
    foreach (var s in scores.Where(s => s.Segment == "TEST" && s.Scope == "OVERALL").OrderBy(s => s.LogLoss))
        Console.WriteLine($"  {Key(s),-40}{s.LogLoss,11:0.000000}{s.Brier,10:0.00000}{s.Rps,10:0.00000}{s.CalibrationError,10:0.00000}{s.Accuracy,10:0.0000}");
    Console.WriteLine();

    // ---------------------------------------------------------------- slices
    var byComp = new List<Score>();
    var byCold = new List<Score>();
    foreach (var v in new[] { variants.First(x => x.method == "NO_CALIBRATION"), selected })
        foreach (var seg in new[] { Segment.Validation, Segment.Test })
        {
            foreach (var g in rawPreds.Select((p, i) => (p, i)).Where(t => t.p.Segment == seg)
                         .GroupBy(t => t.p.CompetitionType).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var idx = g.Select(t => t.i).ToHashSet();
                byComp.Add(Score.Of(Slice(v.probs, idx.Contains), v.method, v.regime,
                    seg.ToString().ToUpperInvariant(), "COMPETITION_TYPE", g.Key, false));
            }
            foreach (var g in rawPreds.Select((p, i) => (p, i)).Where(t => t.p.Segment == seg)
                         .GroupBy(t => t.p.WeakestColdStartClass).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var idx = g.Select(t => t.i).ToHashSet();
                byCold.Add(Score.Of(Slice(v.probs, idx.Contains), v.method, v.regime,
                    seg.ToString().ToUpperInvariant(), "COLD_START_CLASS_WEAKER_SIDE", g.Key, false));
            }
        }

    // ---------------------------------------------------------------- reliability
    var bands = new List<ReliabilityRow>();
    var curve = new List<ReliabilityRow>();
    foreach (var v in variants)
        foreach (var seg in new[] { Segment.Validation, Segment.Test })
        {
            var data = Slice(v.probs, i => samples[i].Segment == seg);
            var segName = seg.ToString().ToUpperInvariant();
            bands.AddRange(CalibrationMetrics.Bands(CalibrationMetrics.AllClassPairs(data), KeyOf(v.method, v.regime), segName, "ALL_CLASSES"));
            if (v.key == "NO_CALIBRATION" || v.key == selectedKey)
            {
                curve.AddRange(CalibrationMetrics.Bands(CalibrationMetrics.AllClassPairs(data), KeyOf(v.method, v.regime), segName, "ALL_CLASSES"));
                curve.AddRange(CalibrationMetrics.Bands(CalibrationMetrics.TopClassPairs(data), KeyOf(v.method, v.regime), segName, "TOP_CLASS_ONLY"));
            }
        }

    // ---------------------------------------------------------------- bootstrap: raw vs selected, on TEST
    var testIdx = Enumerable.Range(0, samples.Count).Where(i => samples[i].Segment == Segment.Test).ToArray();
    var boot = CalibrationBootstrap.Compare(
        testIdx.Select(i => samples[i].Raw).ToList(),
        testIdx.Select(i => selected.probs(i)).ToList(),
        testIdx.Select(i => samples[i].Actual).ToList(),
        "TEST", "all");
    var bootValidation = CalibrationBootstrap.Compare(
        samples.Where(s => s.Segment == Segment.Validation).Select(s => s.Raw).ToList(),
        Enumerable.Range(0, samples.Count).Where(i => samples[i].Segment == Segment.Validation).Select(i => selected.probs(i)).ToList(),
        samples.Where(s => s.Segment == Segment.Validation).Select(s => s.Actual).ToList(),
        "VALIDATION", "all");

    var bootByComp = new List<CalibrationDelta>();
    foreach (var g in rawPreds.Select((p, i) => (p, i)).Where(t => t.p.Segment == Segment.Test)
                 .GroupBy(t => t.p.CompetitionType).OrderBy(g => g.Key, StringComparer.Ordinal))
    {
        var ix = g.Select(t => t.i).ToArray();
        bootByComp.Add(CalibrationBootstrap.Compare(
            ix.Select(i => samples[i].Raw).ToList(),
            ix.Select(i => selected.probs(i)).ToList(),
            ix.Select(i => samples[i].Actual).ToList(),
            "TEST_COMPETITION_TYPE", g.Key));
    }

    // Was the validation gain that drove the choice bigger than its own noise? If its interval
    // already contains zero, the selection was made on a difference the data cannot support.
    Console.WriteLine($"validation gain of the selected method: {bootValidation.DeltaLogLoss:+0.000000;-0.000000;0} " +
                      $"95% CI [{bootValidation.LogLossCiLow:+0.00000;-0.00000;0}, {bootValidation.LogLossCiHigh:+0.00000;-0.00000;0}] " +
                      $"-> {(bootValidation.LogLossSignificant ? "significant on validation" : "NOT significant even on validation")}");
    Console.WriteLine();

    // ---------------------------------------------------------------- verdict
    var verdict = CalibrationVerdict.Decide(winner, rawValidation, testSel, testRaw, boot, bootByComp);
    foreach (var line in verdict.lines) Console.WriteLine(line);
    Console.WriteLine();

    // ---------------------------------------------------------------- outputs
    WriteRawPredictions(Path.Combine(outDir, "raw_predictions.csv"), rawPreds, samples);
    WriteCalibratedPredictions(Path.Combine(outDir, "calibrated_predictions.csv"), rawPreds, samples, runs, selectedKey);
    WriteModelJson(Path.Combine(outDir, "calibration_model.json"), runs, selectedKey, winner, rawValidation, split, ts, validatedTsPath);

    WriteScores(Path.Combine(outDir, "calibration_comparison.csv"), scores);
    WriteScores(Path.Combine(outDir, "calibration_by_competition.csv"), byComp);
    WriteScores(Path.Combine(outDir, "calibration_by_cold_start.csv"), byCold);

    Reporting.WriteLines(Path.Combine(outDir, "calibration_by_probability_band.csv"), ReliabilityRow.CsvHeader,
        bands.Select(b => b.ToCsv()));
    Reporting.WriteLines(Path.Combine(outDir, "reliability_curve.csv"), ReliabilityRow.CsvHeader,
        curve.Select(b => b.ToCsv()));
    Reporting.WriteLines(Path.Combine(outDir, "calibration_bootstrap.csv"), CalibrationDelta.CsvHeader,
        new[] { bootValidation, boot }.Concat(bootByComp).Select(b => b.ToCsv()));

    // ---------------------------------------------------------------- audit
    var audit = CalibrationAudit.Run(samples, runs, variants.Select(v => (v.key, v.probs)).ToList(), split, rawPreds);
    Reporting.WriteLines(Path.Combine(outDir, "calibration_audit.csv"), "Check,Expected,Observed,Status",
        audit.Select(a => $"{Csv(a.check)},{Csv(a.expected)},{Csv(a.observed)},{a.status}"));
    foreach (var a in audit) Console.WriteLine($"  {a.status,-6} {a.check}: expected {a.expected}, observed {a.observed}");

    Console.WriteLine();
    Console.WriteLine($"elapsed {total.Elapsed.TotalSeconds:0.0} s   outputs written to {outDir}");
    return audit.All(a => a.status == "PASS") ? 0 : 3;
}

static string Csv(string s) => s.Contains(',') ? "\"" + s + "\"" : s;
static string Key(Score s) => KeyOf(s.Method, s.Regime);
static string KeyOf(string method, string regime)
    => method == "NO_CALIBRATION" ? method
     : regime == "StaticTrain" ? $"{method}__STATIC_TRAIN" : $"{method}__EXPANDING";

static void WriteScores(string path, IEnumerable<Score> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine(Score.CsvHeader);
    foreach (var r in rows) w.WriteLine(r.ToCsv());
}

static void WriteRawPredictions(string path, IReadOnlyList<MatchPrediction> preds, IReadOnlyList<CalibrationSample> samples)
{
    static string N(double v) => v.ToString("0.00000000", CultureInfo.InvariantCulture);
    static string Q(string s) => s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    using var w = new StreamWriter(path, false);
    w.WriteLine("MatchId,Date,Segment,Season,Competition,CompetitionType,HomeTeam,AwayTeam," +
                "LambdaHome,LambdaAway,RawHomeProbability,RawDrawProbability,RawAwayProbability,RawProbabilitySum," +
                "ActualOutcome,HomeGoals,AwayGoals,WeakestColdStartClass");
    for (var i = 0; i < preds.Count; i++)
    {
        var p = preds[i];
        var r = samples[i].Raw;
        w.WriteLine(string.Join(',', Q(p.MatchId), p.Date.ToString("yyyy-MM-dd"), p.Segment.ToString().ToUpperInvariant(),
            Q(p.Season), Q(p.Competition), Q(p.CompetitionType), Q(p.HomeTeam), Q(p.AwayTeam),
            N(p.LambdaHome), N(p.LambdaAway), N(r.Home), N(r.Draw), N(r.Away), N(r.Sum),
            p.Actual.ToString(), p.HomeGoals.ToString(CultureInfo.InvariantCulture),
            p.AwayGoals.ToString(CultureInfo.InvariantCulture), Q(p.WeakestColdStartClass)));
    }
}

static void WriteCalibratedPredictions(string path, IReadOnlyList<MatchPrediction> preds,
    IReadOnlyList<CalibrationSample> samples, IReadOnlyList<CalibrationRun> runs, string selectedKey)
{
    static string N(double v) => v.ToString("0.00000000", CultureInfo.InvariantCulture);
    static string Q(string s) => s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    var header = new StringBuilder("MatchId,Date,Segment,CompetitionType,WeakestColdStartClass,ActualOutcome," +
                                   "RawHome,RawDraw,RawAway,SelectedHome,SelectedDraw,SelectedAway,SelectedSum,SelectedMethod");
    foreach (var r in runs) header.Append(',').Append(r.Key).Append("_Home,").Append(r.Key).Append("_Draw,").Append(r.Key).Append("_Away");

    var sel = runs.First(r => r.Key == selectedKey);
    using var w = new StreamWriter(path, false);
    w.WriteLine(header.ToString());
    for (var i = 0; i < preds.Count; i++)
    {
        var p = preds[i];
        var raw = samples[i].Raw;
        var s = sel.Calibrated[i];
        var sb = new StringBuilder(400);
        sb.Append(Q(p.MatchId)).Append(',').Append(p.Date.ToString("yyyy-MM-dd")).Append(',')
          .Append(p.Segment.ToString().ToUpperInvariant()).Append(',').Append(Q(p.CompetitionType)).Append(',')
          .Append(Q(p.WeakestColdStartClass)).Append(',').Append(p.Actual.ToString()).Append(',')
          .Append(N(raw.Home)).Append(',').Append(N(raw.Draw)).Append(',').Append(N(raw.Away)).Append(',')
          .Append(N(s.Home)).Append(',').Append(N(s.Draw)).Append(',').Append(N(s.Away)).Append(',')
          .Append(N(s.Sum)).Append(',').Append(selectedKey);
        foreach (var r in runs)
        {
            var c = r.Calibrated[i];
            sb.Append(',').Append(N(c.Home)).Append(',').Append(N(c.Draw)).Append(',').Append(N(c.Away));
        }
        w.WriteLine(sb.ToString());
    }
}

static void WriteModelJson(string path, IReadOnlyList<CalibrationRun> runs, string selectedKey,
    Score winner, Score rawValidation, SplitConfig split, TeamStrengthConfig ts, string tsPath)
{
    static string F(double v) => v.ToString("0.00000000", CultureInfo.InvariantCulture);
    var sb = new StringBuilder();
    sb.AppendLine("{");
    sb.AppendLine("  \"_comment\": \"Calibration models fitted for the FROZEN Independent Poisson V2 baseline. A calibrator sees only (raw probability, outcome) pairs - never a team, a rating or a feature - so nothing here can be a model improvement in disguise. The selected method was chosen on VALIDATION alone; TEST was scored once afterwards.\",");
    sb.AppendLine("  \"calibrationVersion\": \"CALIBRATION_V1\",");
    sb.AppendLine($"  \"baseModel\": \"INDEPENDENT_POISSON_V2\",");
    sb.AppendLine($"  \"baseModelConfig\": \"{tsPath.Replace('\\', '/')}\",");
    sb.AppendLine($"  \"baseModelUnchanged\": \"halfLifeDays={F(ts.HalfLifeDays)}, learningRate={F(ts.LearningRate)}, shrinkageK={F(ts.ShrinkageK)}, ratioSmoothing={F(ts.RatioSmoothing)} - read from the validated config and not re-tuned\",");
    sb.AppendLine($"  \"splitVersion\": \"{split.SplitVersion}\",");
    sb.AppendLine($"  \"selected\": \"{selectedKey}\",");
    sb.AppendLine($"  \"selectionCriterion\": \"lowest VALIDATION log loss, ties broken by Brier, then RPS, then calibration error\",");
    sb.AppendLine($"  \"selectionValidationLogLoss\": {F(winner.LogLoss)},");
    sb.AppendLine($"  \"rawValidationLogLoss\": {F(rawValidation.LogLoss)},");
    sb.AppendLine("  \"methods\": [");

    for (var i = 0; i < runs.Count; i++)
    {
        var r = runs[i];
        var active = r.RefitTrace.Count(t => t.Active);
        sb.AppendLine("    {");
        sb.AppendLine($"      \"key\": \"{r.Key}\",");
        sb.AppendLine($"      \"method\": \"{r.Method}\",");
        sb.AppendLine($"      \"regime\": \"{r.Regime}\",");
        sb.AppendLine($"      \"selected\": {(r.Key == selectedKey ? "true" : "false")},");
        sb.AppendLine($"      \"parameterCount\": {r.FinalModel.ParameterCount},");
        sb.AppendLine($"      \"refits\": {r.RefitTrace.Count}, \"activeRefits\": {active},");
        sb.AppendLine($"      \"latestTrainingDate\": \"{r.LatestTrainingDate?.ToString("yyyy-MM-dd") ?? ""}\",");
        sb.AppendLine($"      \"finalParametersReadable\": \"{r.FinalModel.DescribeParameters().Replace("\"", "'")}\",");
        sb.AppendLine($"      {r.FinalModel.ParametersJson()},");
        sb.Append("      \"refitTrace\": [");
        var shown = r.RefitTrace.Where(t => t.Active).ToList();
        for (var j = 0; j < shown.Count; j++)
        {
            if (j > 0) sb.Append(',');
            sb.AppendLine();
            sb.Append($"        {{ \"effectiveFrom\": \"{shown[j].EffectiveFrom:yyyy-MM-dd}\", \"historySize\": {shown[j].HistorySize}, " +
                      $"\"historyLatestDate\": \"{shown[j].HistoryLatestDate?.ToString("yyyy-MM-dd") ?? ""}\", " +
                      $"\"parameters\": \"{shown[j].Parameters.Replace("\"", "'")}\" }}");
        }
        sb.AppendLine();
        sb.AppendLine("      ]");
        sb.AppendLine(i == runs.Count - 1 ? "    }" : "    },");
    }

    sb.AppendLine("  ]");
    sb.AppendLine("}");
    File.WriteAllText(path, sb.ToString());
}
