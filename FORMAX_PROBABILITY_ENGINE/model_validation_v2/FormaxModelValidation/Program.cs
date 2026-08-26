using System.Globalization;
using System.Text;
using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.ModelValidation.Services;
using Formax.ModelValidation.Tests;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;
using Formax.TeamStrength.Tests;
using static Formax.ModelValidation.ProgramReports;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var root = FindRepoRoot(AppContext.BaseDirectory);
var datasetPath = ArgValue("--dataset") ?? Path.Combine(root, "FORMAX_HISTORICAL_MASTER", "FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv");
var splitPath = ArgValue("--split") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "model_validation_v2", "split.config.json");
var dcConfigPath = ArgValue("--dc-config") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "dixon_coles", "dixoncoles.config.json");
var tsConfigPath = ArgValue("--ts-config") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "team_strength", "teamstrength.config.json");
var snapshotPath = ArgValue("--snapshots") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "team_strength", "output", "team_strength_snapshots.csv");
var outDir = ArgValue("--out") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "model_validation_v2");

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
switch (command)
{
    case "test": return RunTests();
    case "probe": return RunProbe();
    case "run": return RunAll();
    case "all": { var t = RunTests(); return t != 0 ? t : RunAll(); }
    default:
        Console.WriteLine("FormaxModelValidation - team strength validation + Poisson family comparison (research only)");
        Console.WriteLine("  test    leakage, framework-equivalence and model-mathematics tests");
        Console.WriteLine("  probe   time a single candidate evaluation");
        Console.WriteLine("  run     full validation: parameter search on TRAIN/VALIDATION, one scoring pass on TEST");
        Console.WriteLine("  all     test, then run");
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
    ValidationTests.Register(runner,
        File.Exists(datasetPath) ? datasetPath : null,
        File.Exists(snapshotPath) ? snapshotPath : null,
        File.Exists(splitPath) ? splitPath : null,
        File.Exists(dcConfigPath) ? dcConfigPath : null,
        File.Exists(tsConfigPath) ? tsConfigPath : null);
    if (!File.Exists(datasetPath)) Console.WriteLine("  (real-data tests skipped: dataset not found)");
    return runner.Run();
}

(List<MatchRecord> matches, SplitConfig split, DixonColesConfig dc, TeamStrengthConfig ts) LoadAll()
{
    var hygiene = File.Exists(tsConfigPath) ? TeamStrengthConfig.Load(tsConfigPath) : new TeamStrengthConfig();
    var read = Formax.TeamStrength.Services.MatchCsvReader.Read(datasetPath, hygiene);
    var split = SplitConfig.Load(splitPath);
    var dc = File.Exists(dcConfigPath) ? DixonColesConfig.Load(dcConfigPath) : new DixonColesConfig();
    return (read.Matches, split, dc, hygiene);
}

int RunProbe()
{
    var (matches, split, dc, ts) = LoadAll();
    var fence = new TestFence(split);
    var tuner = new Tuner(matches, split, dc, fence);
    Console.WriteLine($"matches {matches.Count}, selectable {tuner.SelectableMatches}");

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var s = tuner.Evaluate(StrengthPipeline.Copy(ts), new ModelParameters(),
        ModelMask.Simple | ModelMask.TeamStrength | ModelMask.IndependentPoisson);
    sw.Stop();
    Console.WriteLine($"one evaluation: {sw.ElapsedMilliseconds} ms   validation N={s.Metrics.N} logloss={s.Metrics.LogLoss:0.00000}");
    Console.WriteLine($"primary grid would be {SearchGrid.PrimaryGridSize} evaluations " +
                      $"(~{SearchGrid.PrimaryGridSize * sw.ElapsedMilliseconds / 1000.0 / Environment.ProcessorCount:0} s on {Environment.ProcessorCount} cores)");
    return 0;
}

int RunAll()
{
    var total = System.Diagnostics.Stopwatch.StartNew();
    var (matches, split, dc, tsSeed) = LoadAll();
    Directory.CreateDirectory(outDir);

    const ModelMask TuningMask = ModelMask.Simple | ModelMask.TeamStrength | ModelMask.IndependentPoisson;

    Console.WriteLine("== FORMAX MODEL BASELINE V2 - TEAM STRENGTH VALIDATION + POISSON FAMILY COMPARISON ==");
    Console.WriteLine($"dataset : {datasetPath}   matches: {matches.Count}");
    Console.WriteLine($"split   : {split.Describe()}");
    Console.WriteLine($"          {split.DescribeBoundaries(matches)}");
    foreach (var g in matches.GroupBy(m => split.Of(m.Date)).OrderBy(g => g.Key))
        Console.WriteLine($"          {g.Key,-10} {g.Count(),6} matches   {g.Min(m => m.Date):yyyy-MM-dd} .. {g.Max(m => m.Date):yyyy-MM-dd}");
    Console.WriteLine("NO parameter is chosen on TEST. The test segment is scored once, at the end.");
    Console.WriteLine();

    var fence = new TestFence(split);
    var tuner = new Tuner(matches, split, dc, fence);

    // ================================================================= stage 1-3: team strength
    var best = StrengthPipeline.Copy(tsSeed);
    var mp = new ModelParameters { Rho = 0.0, BivariateC = 0.0 };
    double bestLoss = double.NaN;
    var converged = false;
    var rounds = 0;

    for (var round = 1; round <= SearchGrid.MaxRounds; round++)
    {
        rounds = round;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // ---- primary grid
        var primary = new List<TeamStrengthConfig>();
        foreach (var hl in SearchGrid.HalfLifeDays)
            foreach (var lr in SearchGrid.LearningRate)
                foreach (var k in SearchGrid.ShrinkageK)
                {
                    var c = StrengthPipeline.Copy(best);
                    c.HalfLifeDays = hl; c.LearningRate = lr; c.ShrinkageK = k;
                    c.Validate();
                    primary.Add(c);
                }

        var ranked = tuner.Sweep($"S1_PRIMARY_GRID_ROUND{round}", "halfLifeDays x learningRate x shrinkageK",
            primary, mp, TuningMask);
        var top = ranked[0];
        Console.WriteLine($"round {round} primary grid: {primary.Count} candidates in {sw.ElapsedMilliseconds} ms  -> " +
                          $"halfLife={top.cfg.HalfLifeDays} lr={top.cfg.LearningRate} k={top.cfg.ShrinkageK} " +
                          $"validation logloss={top.scored.Metrics.LogLoss:0.000000}");

        var moved = Math.Abs(top.cfg.HalfLifeDays - best.HalfLifeDays) > 1e-9
                 || Math.Abs(top.cfg.LearningRate - best.LearningRate) > 1e-12
                 || Math.Abs(top.cfg.ShrinkageK - best.ShrinkageK) > 1e-12;
        best = StrengthPipeline.Copy(top.cfg);
        bestLoss = top.scored.Metrics.LogLoss;

        // ---- secondary coordinates, one at a time
        var secondaryMoved = false;

        var ratio = tuner.Sweep($"S2_RATIO_SMOOTHING_ROUND{round}", "ratioSmoothing",
            SearchGrid.RatioSmoothing.Select(v => { var c = StrengthPipeline.Copy(best); c.RatioSmoothing = v; c.Validate(); return c; }),
            mp, TuningMask);
        if (Math.Abs(ratio[0].cfg.RatioSmoothing - best.RatioSmoothing) > 1e-12) secondaryMoved = true;
        best = StrengthPipeline.Copy(ratio[0].cfg); bestLoss = ratio[0].scored.Metrics.LogLoss;

        var baseline = tuner.Sweep($"S2_MIN_BASELINE_SAMPLES_ROUND{round}", "minBaselineSamples",
            SearchGrid.MinBaselineSamples.Select(v => { var c = StrengthPipeline.Copy(best); c.MinBaselineSamples = v; c.Validate(); return c; }),
            mp, TuningMask);
        if (baseline[0].cfg.MinBaselineSamples != best.MinBaselineSamples) secondaryMoved = true;
        best = StrengthPipeline.Copy(baseline[0].cfg); bestLoss = baseline[0].scored.Metrics.LogLoss;

        var pool = tuner.Sweep($"S2_PRIOR_POOL_ROUND{round}", "useCompetitionTypePool",
            SearchGrid.UseCompetitionTypePool.Select(v => { var c = StrengthPipeline.Copy(best); c.UseCompetitionTypePool = v; c.Validate(); return c; }),
            mp, TuningMask);
        if (pool[0].cfg.UseCompetitionTypePool != best.UseCompetitionTypePool) secondaryMoved = true;
        best = StrengthPipeline.Copy(pool[0].cfg); bestLoss = pool[0].scored.Metrics.LogLoss;

        var clamp = tuner.Sweep($"S2_INDEX_CLAMP_ROUND{round}", "minIndex/maxIndex",
            SearchGrid.IndexClamp.Select(v => { var c = StrengthPipeline.Copy(best); c.MinIndex = v.min; c.MaxIndex = v.max; c.Validate(); return c; }),
            mp, TuningMask);
        if (Math.Abs(clamp[0].cfg.MaxIndex - best.MaxIndex) > 1e-12) secondaryMoved = true;
        best = StrengthPipeline.Copy(clamp[0].cfg); bestLoss = clamp[0].scored.Metrics.LogLoss;

        Console.WriteLine($"round {round} secondary: ratioSmoothing={best.RatioSmoothing} minBaselineSamples={best.MinBaselineSamples} " +
                          $"pool={best.UseCompetitionTypePool} clamp=[{best.MinIndex},{best.MaxIndex}] " +
                          $"validation logloss={bestLoss:0.000000}  ({sw.ElapsedMilliseconds} ms)");

        if (!moved && !secondaryMoved) { converged = true; Console.WriteLine($"round {round}: nothing moved - search converged."); break; }
    }

    if (!converged)
        Console.WriteLine($"WARNING: the search was still moving when it hit its round limit ({SearchGrid.MaxRounds}). " +
                          "The reported config is the best found, NOT a converged optimum.");

    var gridEdges = SearchGrid.ValuesOnGridEdge(best.HalfLifeDays, best.LearningRate, best.ShrinkageK,
        best.RatioSmoothing, best.MinBaselineSamples, best.MinIndex, best.MaxIndex);
    if (gridEdges.Count > 0)
        Console.WriteLine($"WARNING: selected on the edge of the candidate range: {string.Join("; ", gridEdges)}. " +
                          "The optimum may lie outside the search space.");

    // venue shrinkage is not identifiable from these models: prove it instead of claiming it
    var venueProbe = tuner.Sweep("S2_VENUE_SHRINKAGE_DIAGNOSTIC", "venueShrinkageK",
        new[] { 1.0, 4.0, 16.0 }.Select(v => { var c = StrengthPipeline.Copy(best); c.VenueShrinkageK = v; c.Validate(); return c; }),
        mp, TuningMask);
    var venueSpread = venueProbe.Max(r => r.scored.Metrics.LogLoss) - venueProbe.Min(r => r.scored.Metrics.LogLoss);

    Console.WriteLine($"venue shrinkage diagnostic: log loss spread over k in 1/4/16 = {venueSpread:0.00000000} " +
                      (venueSpread < 1e-12 ? "(no effect - not identifiable from 1X2 models)" : "(HAS an effect)"));
    Console.WriteLine();

    // ================================================================= stage 4-5: dependence
    var cache = tuner.CacheLambdas(best);
    var valCount = cache.Count(c => c.Seg == Segment.Validation);
    var trainCount = cache.Count(c => c.Seg == Segment.Train);
    Console.WriteLine($"lambda cache: {cache.Count} matches (train {trainCount}, validation {valCount}, test 0 by construction)");

    var rhoSweep = DependenceSearch.SweepRho(cache, dc,
        DependenceSearch.Grid(SearchGrid.RhoFrom, SearchGrid.RhoTo, SearchGrid.RhoStep));
    var rhoByValidation = rhoSweep.OrderBy(p => p.ValidationLogLoss).First();
    var rhoByTrainMle = rhoSweep.OrderByDescending(p => p.TrainScoreLogLik).First();
    Console.WriteLine($"rho: validation-selected = {rhoByValidation.Value:0.00} (val logloss {rhoByValidation.ValidationLogLoss:0.000000})   " +
                      $"train MLE = {rhoByTrainMle.Value:0.00} (train score loglik {rhoByTrainMle.TrainScoreLogLik:0.000000})");

    var bpProp = DependenceSearch.SweepBivariate(cache, dc, BivariateMode.Proportional,
        DependenceSearch.Grid(SearchGrid.BivariateProportionalFrom, SearchGrid.BivariateProportionalTo, SearchGrid.BivariateProportionalStep));
    var bpConst = DependenceSearch.SweepBivariate(cache, dc, BivariateMode.Constant,
        DependenceSearch.Grid(SearchGrid.BivariateConstantFrom, SearchGrid.BivariateConstantTo, SearchGrid.BivariateConstantStep));
    var bpPropBest = bpProp.OrderBy(p => p.ValidationLogLoss).First();
    var bpConstBest = bpConst.OrderBy(p => p.ValidationLogLoss).First();
    var bpMode = bpPropBest.ValidationLogLoss <= bpConstBest.ValidationLogLoss ? BivariateMode.Proportional : BivariateMode.Constant;
    var bpBest = bpMode == BivariateMode.Proportional ? bpPropBest : bpConstBest;
    var bpTrainMle = (bpMode == BivariateMode.Proportional ? bpProp : bpConst).OrderByDescending(p => p.TrainScoreLogLik).First();
    Console.WriteLine($"bivariate: proportional c={bpPropBest.Value:0.00} ({bpPropBest.ValidationLogLoss:0.000000})   " +
                      $"constant l3={bpConstBest.Value:0.00} ({bpConstBest.ValidationLogLoss:0.000000})   " +
                      $"-> {bpMode} c={bpBest.Value:0.00}   train MLE c={bpTrainMle.Value:0.00}");
    Console.WriteLine();

    var validated = new ModelParameters
    {
        Rho = rhoByValidation.Value,
        BivariateMode = bpMode,
        BivariateC = bpBest.Value
    };

    Console.WriteLine($"TEST FENCE: {fence.Describe()}");
    Console.WriteLine($"parameter search evaluations: {tuner.Evaluations}");
    Console.WriteLine();

    // ================================================================= final scoring pass
    var v1Ts = File.Exists(tsConfigPath) ? TeamStrengthConfig.Load(tsConfigPath) : new TeamStrengthConfig();
    var v1Params = new ModelParameters { Rho = dc.Rho, BivariateMode = BivariateMode.Proportional, BivariateC = 0.0 };

    var validatedPreds = FinalRun(best, validated, matches, dc, split, "VALIDATED_V2");
    var v1Preds = FinalRun(v1Ts, v1Params, matches, dc, split, "UNVALIDATED_V1");

    // ================================================================= outputs
    var metricRows = new List<MetricRow>();
    foreach (var (setName, preds) in new[] { ("VALIDATED_V2", validatedPreds), ("UNVALIDATED_V1", v1Preds) })
    {
        metricRows.AddRange(Reporting.Score(preds, setName, "FULL", "OVERALL", "all"));
        foreach (var seg in new[] { Segment.Train, Segment.Validation, Segment.Test })
        {
            var slice = preds.Where(p => p.Segment == seg).ToList();
            metricRows.AddRange(Reporting.Score(slice, setName, Reporting.SegName(seg), "OVERALL", "all"));
            metricRows.AddRange(Reporting.Score(slice.Where(p => !p.IsColdStart).ToList(), setName,
                Reporting.SegName(seg), "OVERALL", "excluding cold start"));
        }
    }
    Reporting.WriteMetrics(Path.Combine(outDir, "model_comparison.csv"), metricRows);

    var compRows = new List<MetricRow>();
    foreach (var (setName, preds) in new[] { ("VALIDATED_V2", validatedPreds), ("UNVALIDATED_V1", v1Preds) })
        foreach (var seg in new[] { Segment.Train, Segment.Validation, Segment.Test })
        {
            var slice = preds.Where(p => p.Segment == seg).ToList();
            compRows.AddRange(Reporting.ScoreBy(slice, setName, Reporting.SegName(seg), "COMPETITION_TYPE", p => p.CompetitionType));
            compRows.AddRange(Reporting.ScoreBy(slice.Where(p => p.CompetitionType == "DOMESTIC_LEAGUE").ToList(),
                setName, Reporting.SegName(seg), "COMPETITION", p => p.Competition));
        }
    Reporting.WriteMetrics(Path.Combine(outDir, "metrics_by_competition.csv"), compRows);

    var coldRows = new List<MetricRow>();
    foreach (var (setName, preds) in new[] { ("VALIDATED_V2", validatedPreds), ("UNVALIDATED_V1", v1Preds) })
        foreach (var seg in new[] { Segment.Train, Segment.Validation, Segment.Test })
        {
            var slice = preds.Where(p => p.Segment == seg).ToList();
            coldRows.AddRange(Reporting.ScoreBy(slice, setName, Reporting.SegName(seg),
                "COLD_START_CLASS_WEAKER_SIDE", p => p.WeakestColdStartClass));
            coldRows.AddRange(Reporting.ScoreBy(slice, setName, Reporting.SegName(seg),
                "COLD_START_FLAG", p => p.IsColdStart ? "cold start (a side has no history)" : "both sides have history"));
            // the breakdown the task asks for explicitly: UEFA qualifier, by cold-start class
            coldRows.AddRange(Reporting.ScoreBy(slice.Where(p => p.CompetitionType == "UEFA_QUALIFIER").ToList(),
                setName, Reporting.SegName(seg), "UEFA_QUALIFIER_BY_COLD_START", p => p.WeakestColdStartClass));
            coldRows.AddRange(Reporting.ScoreBy(slice.Where(p => p.CompetitionType == "UEFA_QUALIFICATION_PLAYOFF").ToList(),
                setName, Reporting.SegName(seg), "UEFA_QPO_BY_COLD_START", p => p.WeakestColdStartClass));
            coldRows.AddRange(Reporting.ScoreBy(slice, setName, Reporting.SegName(seg), "PRIOR_POOL", p =>
            {
                var h = p.HomePriorSource.StartsWith("POOL:UEFA", StringComparison.Ordinal);
                var a = p.AwayPriorSource.StartsWith("POOL:UEFA", StringComparison.Ordinal);
                return h && a ? "both sides pooled against UEFA" : h || a ? "one side pooled against UEFA" : "neither side pooled against UEFA";
            }));
        }
    Reporting.WriteMetrics(Path.Combine(outDir, "metrics_by_cold_start.csv"), coldRows);

    var seasonRows = new List<MetricRow>();
    foreach (var (setName, preds) in new[] { ("VALIDATED_V2", validatedPreds), ("UNVALIDATED_V1", v1Preds) })
        foreach (var g in preds.GroupBy(p => p.Season).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var seg = Reporting.SegName(g.First().Segment);
            var mixed = g.Select(p => p.Segment).Distinct().Count() > 1 ? "MIXED" : seg;
            seasonRows.AddRange(Reporting.Score(g.ToList(), setName, mixed, "SEASON", g.Key));
        }
    Reporting.WriteMetrics(Path.Combine(outDir, "metrics_by_season.csv"), seasonRows);

    // ---- predictions
    using (var w = new StreamWriter(Path.Combine(outDir, "predictions_all_models.csv"), false))
    {
        w.WriteLine(MatchPrediction.CsvHeader);
        foreach (var p in validatedPreds) w.WriteLine(p.ToCsv());
    }

    // ---- tuning trace
    Reporting.WriteLines(Path.Combine(outDir, "tuning_trace.csv"), TuningRecord.CsvHeader,
        tuner.Trace.Select(t => t.ToCsv()));

    // ---- dependence sweeps
    var depLines = new List<string>();
    foreach (var p in rhoSweep) depLines.Add(DepCsv("DIXON_COLES", "rho", p, Math.Abs(p.Value - validated.Rho) < 1e-9));
    foreach (var p in bpProp) depLines.Add(DepCsv("BIVARIATE_POISSON", "c(proportional)", p,
        bpMode == BivariateMode.Proportional && Math.Abs(p.Value - validated.BivariateC) < 1e-9));
    foreach (var p in bpConst) depLines.Add(DepCsv("BIVARIATE_POISSON", "lambda3(constant)", p,
        bpMode == BivariateMode.Constant && Math.Abs(p.Value - validated.BivariateC) < 1e-9));
    Reporting.WriteLines(Path.Combine(outDir, "dependence_sweep.csv"),
        "Model,Parameter,Value,ValidationN,ValidationLogLoss,ValidationBrier,ValidationRPS,ValidationAccuracy,TrainN,TrainScoreLogLik,Selected",
        depLines);

    // ---- significance
    var testPreds = validatedPreds.Where(p => p.Segment == Segment.Test).ToList();
    var pairs = new List<PairComparison>();
    var order = new[] { ModelId.TeamStrength, ModelId.IndependentPoisson, ModelId.DixonColes, ModelId.BivariatePoisson };
    foreach (var a in order)
        foreach (var b in order)
        {
            if (a >= b) continue;
            pairs.Add(PairedBootstrap.Compare(testPreds, a, b, "TEST", "all"));
        }
    foreach (var a in order) pairs.Add(PairedBootstrap.Compare(testPreds, a, ModelId.Simple, "TEST", "vs naive reference"));
    foreach (var g in testPreds.GroupBy(p => p.Season).OrderBy(g => g.Key, StringComparer.Ordinal))
        foreach (var a in order)
        {
            if (a == ModelId.IndependentPoisson) continue;
            pairs.Add(PairedBootstrap.Compare(g.ToList(), a, ModelId.IndependentPoisson, "TEST_SEASON", g.Key));
        }
    foreach (var g in testPreds.GroupBy(p => p.CompetitionType).OrderBy(g => g.Key, StringComparer.Ordinal))
        foreach (var a in order)
        {
            if (a == ModelId.IndependentPoisson) continue;
            pairs.Add(PairedBootstrap.Compare(g.ToList(), a, ModelId.IndependentPoisson, "TEST_COMPETITION_TYPE", g.Key));
        }
    Reporting.WriteLines(Path.Combine(outDir, "significance_bootstrap.csv"), PairComparison.CsvHeader,
        pairs.Select(p => p.ToCsv()));

    // ---- validated config + parameter provenance
    WriteValidatedConfig(Path.Combine(outDir, "validated_teamstrength_config.json"), best, validated, split,
        tuner.Evaluations, venueSpread, converged, gridEdges);
    WriteParameterTable(Path.Combine(outDir, "validation_parameters.csv"), best, v1Ts, validated, v1Params, split,
        fence, rhoByValidation, rhoByTrainMle, bpBest, bpTrainMle, bpMode, venueSpread, valCount, trainCount,
        converged, gridEdges);

    // ---- leakage audit
    var audit = LeakageAudit(validatedPreds, cache, fence, split, matches,
        perturbed => FinalRun(best, validated, perturbed, dc, split, "PERTURBATION_AUDIT"));
    Reporting.WriteLines(Path.Combine(outDir, "leakage_audit.csv"), "Check,Expected,Observed,Status",
        audit.Select(a => $"{Csv(a.check)},{Csv(a.expected)},{Csv(a.observed)},{a.status}"));

    // ================================================================= verdicts
    Console.WriteLine("TEST SEGMENT - the numbers that count (raw probabilities, no calibration)");
    Console.WriteLine($"{"model",-28}{"N",8}{"LogLoss",12}{"Brier",11}{"RPS",11}{"Accuracy",11}");
    foreach (var m in ModelIds.All)
    {
        var r = metricRows.First(x => x.ParameterSet == "VALIDATED_V2" && x.Segment == "TEST"
            && x.Group == "all" && x.Acc.ModelVersion == ModelIds.Name(m));
        Console.WriteLine($"{r.Acc.ModelVersion,-28}{r.Acc.N,8}{r.Acc.LogLoss,12:0.000000}{r.Acc.Brier,11:0.00000}{r.Acc.Rps,11:0.00000}{r.Acc.Accuracy,11:0.0000}");
    }
    Console.WriteLine();

    var verdicts = Verdicts(testPreds, pairs);
    foreach (var v in verdicts) Console.WriteLine(v);
    Console.WriteLine();

    foreach (var a in audit) Console.WriteLine($"  {a.status,-6} {a.check}: expected {a.expected}, observed {a.observed}");
    Console.WriteLine();
    Console.WriteLine($"elapsed {total.Elapsed.TotalSeconds:0.0} s   outputs written to {outDir}");
    return audit.All(a => a.status == "PASS") ? 0 : 3;
}

static string Csv(string s) => s.Contains(',') ? "\"" + s + "\"" : s;

static string DepCsv(string model, string parameter, DependencePoint p, bool selected)
{
    static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);
    return string.Join(',', model, parameter, F(p.Value),
        p.ValidationN.ToString(CultureInfo.InvariantCulture), F(p.ValidationLogLoss), F(p.ValidationBrier),
        F(p.ValidationRps), F(p.ValidationAccuracy),
        p.TrainN.ToString(CultureInfo.InvariantCulture), F(p.TrainScoreLogLik), selected ? "True" : "False");
}

static List<MatchPrediction> FinalRun(TeamStrengthConfig ts, ModelParameters mp,
    IReadOnlyList<MatchRecord> matches, DixonColesConfig dc, SplitConfig split, string label)
{
    var rows = StrengthPipeline.BuildRows(ts, matches);
    var preds = new List<MatchPrediction>(matches.Count);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var rep = new BacktestV2(dc, mp, split).Run(matches, rows, ModelMask.All, preds.Add);
    sw.Stop();
    Console.WriteLine($"final run [{label}]: {rep.MatchesPredicted} predictions in {sw.ElapsedMilliseconds} ms  " +
                      $"skipped {rep.MatchesSkippedNoSnapshot}  leakage {rep.LeakageViolations}  normalisation {rep.NormalisationViolations}");
    return preds;
}
