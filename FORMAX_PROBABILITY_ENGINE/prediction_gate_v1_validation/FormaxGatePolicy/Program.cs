using System.Globalization;
using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;
using Formax.GatePolicy.Models;
using Formax.GatePolicy.Services;
using Formax.GatePolicy.Tests;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.ModelValidation.Services;
using Formax.Prediction.Config;
using Formax.Prediction.Services;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;
using Formax.TeamStrength.Services;
using Formax.TeamStrength.Tests;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var root = FindRepoRoot(AppContext.BaseDirectory);
var datasetPath = ArgValue("--dataset") ?? Path.Combine(root, "FORMAX_HISTORICAL_MASTER", "FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv");
var splitPath = ArgValue("--split") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "model_validation_v2", "split.config.json");
var dcConfigPath = ArgValue("--dc-config") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "dixon_coles", "dixoncoles.config.json");
var validatedTsPath = ArgValue("--ts-config") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "model_validation_v2", "validated_teamstrength_config.json");
var gateConfigPath = ArgValue("--gate-config") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "prediction_gate_v1", "gate.config.json");
var outDir = ArgValue("--out") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "prediction_gate_v1_validation");

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
switch (command)
{
    case "test": return RunTests();
    case "run": return RunAll();
    case "all": { var t = RunTests(); return t != 0 ? t : RunAll(); }
    default:
        Console.WriteLine("FormaxGatePolicy - prediction gate policy validation (research only)");
        Console.WriteLine("  test   policy, selective-risk and invariance tests");
        Console.WriteLine("  run    sweep policies on VALIDATION, choose one, score it once on TEST");
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
    PolicyTests.Register(runner,
        File.Exists(datasetPath) ? datasetPath : null,
        File.Exists(splitPath) ? splitPath : null,
        File.Exists(dcConfigPath) ? dcConfigPath : null,
        File.Exists(validatedTsPath) ? validatedTsPath : null,
        File.Exists(gateConfigPath) ? gateConfigPath : null);
    if (!File.Exists(datasetPath)) Console.WriteLine("  (real-data tests skipped: dataset not found)");
    return runner.Run();
}

int RunAll()
{
    var total = System.Diagnostics.Stopwatch.StartNew();
    Directory.CreateDirectory(outDir);

    var ts = TeamStrengthConfig.Load(validatedTsPath);
    var dc = DixonColesConfig.Load(dcConfigPath);
    var split = SplitConfig.Load(splitPath);
    var baselineGate = GateConfig.Load(gateConfigPath);
    var matches = MatchCsvReader.Read(datasetPath, ts).Matches;

    Console.WriteLine("== FORMAX PREDICTION GATE POLICY VALIDATION V1 ==");
    Console.WriteLine($"model     : {baselineGate.ModelVersion} on {baselineGate.TeamStrengthVersion} - FROZEN");
    Console.WriteLine($"baseline  : {PolicyGrid.Current.Name} -> {PolicyGrid.Current.Describe()}");
    Console.WriteLine($"gate cfg  : {gateConfigPath}");
    Console.WriteLine($"policies  : {PolicyGrid.All().Count()} candidates " +
                      $"(minPriorMatches {string.Join('/', PolicyGrid.MinPriorMatches)} x " +
                      $"minCompetitionMatches {string.Join('/', PolicyGrid.MinCompetitionMatches)})");

    // ---------------------------------------------------------------- the frozen model, once
    var built = new TeamStrengthService(ts).Build(matches);
    var snapshots = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side));
    var rows = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side), StrengthPipeline.ToRow);

    var rawPreds = new List<MatchPrediction>(matches.Count);
    const ModelMask mask = ModelMask.Simple | ModelMask.TeamStrength | ModelMask.IndependentPoisson;
    new BacktestV2(dc, new ModelParameters { Rho = 0.0, BivariateC = 0.0 }, split)
        .Run(matches, rows, mask, rawPreds.Add);

    var coverage = CompetitionCoverage.Build(matches);
    var identity = matches.ToDictionary(m => m.MatchId, m => m.IdentityConfidence, StringComparer.Ordinal);

    var ctx = new List<MatchContext>(rawPreds.Count);
    foreach (var raw in rawPreds)
    {
        snapshots.TryGetValue((raw.MatchId, "HOME"), out var h);
        snapshots.TryGetValue((raw.MatchId, "AWAY"), out var a);
        var p = raw.Probabilities[(int)ModelId.IndependentPoisson];
        ctx.Add(new MatchContext
        {
            Raw = raw,
            Home = h,
            Away = a,
            IdentityConfidence = identity[raw.MatchId],
            CompetitionMatchesObserved = coverage.For(raw.MatchId),
            Segment = raw.Segment,
            WeakestColdStartClass = raw.WeakestColdStartClass,
            ModelLogLoss = -Math.Log(Math.Max(p[raw.Actual], 1e-15))
        });
    }
    Console.WriteLine($"matches   : {ctx.Count}   validation {ctx.Count(c => c.Segment == Segment.Validation)}   " +
                      $"test {ctx.Count(c => c.Segment == Segment.Test)}");
    Console.WriteLine();

    // ---------------------------------------------------------------- sweep, VALIDATION only
    bool IsValidation(MatchContext c) => c.Segment == Segment.Validation;
    var validationLosses = ctx.Where(IsValidation).Select(c => c.ModelLogLoss).ToList();
    var validationIdx = Enumerable.Range(0, ctx.Count).Where(i => IsValidation(ctx[i])).ToArray();

    var comparison = new List<PolicyResult>();
    var detailed = new List<PolicyResult>();
    var selection = new List<SelectionTest.Result>();
    var decisions = new Dictionary<string, bool[]>(StringComparer.Ordinal);

    foreach (var policy in PolicyGrid.All())
    {
        var published = PolicyEvaluator.Decide(ctx, policy, baselineGate);
        decisions[policy.Name] = published;

        comparison.Add(PolicyEvaluator.Measure(ctx, published, policy, "VALIDATION", "OVERALL", "all", IsValidation));

        foreach (var cls in new[] { "NoHistory", "Limited", "Developing", "Established", "Rich" })
            detailed.Add(PolicyEvaluator.Measure(ctx, published, policy, "VALIDATION", "COLD_START_CLASS_WEAKER_SIDE", cls,
                c => IsValidation(c) && c.WeakestColdStartClass == cls));

        foreach (var comp in ctx.Where(IsValidation).Select(c => c.Raw.CompetitionType).Distinct().OrderBy(x => x, StringComparer.Ordinal))
            detailed.Add(PolicyEvaluator.Measure(ctx, published, policy, "VALIDATION", "COMPETITION_TYPE", comp,
                c => IsValidation(c) && c.Raw.CompetitionType == comp));

        var mask2 = validationIdx.Select(i => published[i]).ToArray();
        selection.Add(SelectionTest.Run(validationLosses, mask2, policy.Name, "VALIDATION"));
    }

    // ---------------------------------------------------------------- 11. model metric vs gate metric
    var modelLogLosses = comparison.Select(r => r.AllLogLoss).Distinct().ToList();
    Console.WriteLine("MODEL QUALITY - unchanged by every policy, by construction");
    Console.WriteLine($"  VALIDATION log loss over ALL matches: {modelLogLosses[0]:0.000000}   " +
                      $"distinct values across the {comparison.Count} policies: {modelLogLosses.Count}" +
                      (modelLogLosses.Count == 1 ? "  (the gate cannot touch the model)" : "  ** POLICY CHANGED THE MODEL **"));
    Console.WriteLine();

    Console.WriteLine("GATE QUALITY on VALIDATION - coverage against selective risk");
    Console.WriteLine($"{"policy",-12}{"minPrior",9}{"minComp",9}{"published",11}{"coverage",10}" +
                      $"{"pubLogLoss",12}{"rejLogLoss",12}{"vs random",26}");
    foreach (var r in comparison.OrderByDescending(r => r.Coverage).ThenBy(r => r.Policy.Name, StringComparer.Ordinal))
    {
        var s = selection.First(x => x.Policy == r.Policy.Name);
        var marker = r.Policy == PolicyGrid.Current ? " <- current" : "";
        Console.WriteLine($"{r.Policy.Name,-12}{r.Policy.MinPriorMatches,9}{r.Policy.MinCompetitionMatches,9}" +
                          $"{r.Published,11}{r.Coverage * 100,9:0.00}%{r.PublishedLogLoss,12:0.000000}" +
                          $"{r.RejectedLogLoss,12:0.000000}  {Short(s.Verdict),-24}{marker}");
    }
    Console.WriteLine();

    // ---------------------------------------------------------------- 13. selection, VALIDATION only
    var current = comparison.First(r => r.Policy == PolicyGrid.Current);
    var currentSelection = selection.First(s => s.Policy == PolicyGrid.Current.Name);

    const double MeaningfulLogLoss = 0.0010;
    const double MinCoverage = 0.95;
    const double MinCompetitionCoverage = 0.80;

    Console.WriteLine("SELECTION RULE (declared before the sweep, applied mechanically):");
    Console.WriteLine($"  1. VALIDATION published log loss must beat the current policy by at least {MeaningfulLogLoss:0.0000}");
    Console.WriteLine($"  2. coverage must stay at or above {MinCoverage:0.00}");
    Console.WriteLine("  3. the refusals must beat random refusal of the same size (p < 0.05)");
    Console.WriteLine($"  4. no competition type with 100+ matches may fall below {MinCompetitionCoverage:0.00} coverage " +
                      "unless it was already below it under the current policy");
    Console.WriteLine();

    var candidates = new List<(PolicyResult r, SelectionTest.Result s, List<string> failures)>();
    foreach (var r in comparison)
    {
        if (r.Policy == PolicyGrid.Current) continue;
        var s = selection.First(x => x.Policy == r.Policy.Name);
        var failures = new List<string>();

        if (!(current.PublishedLogLoss - r.PublishedLogLoss >= MeaningfulLogLoss))
            failures.Add($"gain {current.PublishedLogLoss - r.PublishedLogLoss:+0.000000;-0.000000;0} below threshold");
        if (r.Coverage < MinCoverage) failures.Add($"coverage {r.Coverage * 100:0.00}%");
        if (!(s.PValue < 0.05)) failures.Add($"selection p={s.PValue:0.000}");

        foreach (var comp in detailed.Where(d => d.Policy == r.Policy && d.Scope == "COMPETITION_TYPE" && d.Total >= 100))
        {
            var underCurrent = detailed.First(d => d.Policy == PolicyGrid.Current && d.Scope == "COMPETITION_TYPE" && d.Group == comp.Group);
            if (comp.Coverage < MinCompetitionCoverage && underCurrent.Coverage >= MinCompetitionCoverage)
                failures.Add($"{comp.Group} coverage {comp.Coverage * 100:0.0}%");
        }

        candidates.Add((r, s, failures));
    }

    var passing = candidates.Where(c => c.failures.Count == 0)
        .OrderBy(c => c.r.PublishedLogLoss).ToList();

    Policy chosen;
    string verdict, recommendation;
    if (passing.Count == 0)
    {
        chosen = PolicyGrid.Current;
        verdict = "GATE_POLICY_IMPROVEMENT_NOT_PROVEN";
        recommendation = "KEEP_CURRENT_GATE";
        Console.WriteLine($"No candidate satisfied the rule. Closest attempts:");
        foreach (var c in candidates.OrderBy(c => c.r.PublishedLogLoss).Take(4))
            Console.WriteLine($"  {c.r.Policy.Name,-12} pubLogLoss {c.r.PublishedLogLoss:0.000000}  " +
                              $"coverage {c.r.Coverage * 100:0.00}%  failed: {string.Join("; ", c.failures)}");
    }
    else
    {
        chosen = passing[0].r.Policy;
        verdict = "GATE_POLICY_IMPROVEMENT_PROVEN";
        recommendation = $"RECOMMENDED_GATE_POLICY = {chosen.Name} ({chosen.Describe()})";
        Console.WriteLine($"{passing.Count} candidate(s) satisfied the rule; best is {chosen.Name}.");
    }

    Console.WriteLine();
    Console.WriteLine($"VERDICT: {verdict}");
    Console.WriteLine($"         {recommendation}");
    Console.WriteLine();
    Console.WriteLine("CURRENT POLICY, examined closely on VALIDATION");
    Console.WriteLine($"  coverage {current.Coverage * 100:0.00}%  ({current.Published} published, {current.Rejected} refused)");
    Console.WriteLine($"  kept    : {current.PublishedLogLoss:0.000000}   a random keep of the same size would land in " +
                      $"[{currentSelection.NullP025:0.000000}, {currentSelection.NullP975:0.000000}]   p={currentSelection.PValue:0.000}");
    Console.WriteLine($"  refused : {currentSelection.ActualRejectedLogLoss:0.000000}   a random refusal of the same size would land in " +
                      $"[{currentSelection.RejectedNullP025:0.000000}, {currentSelection.RejectedNullP975:0.000000}]" +
                      (currentSelection.RefusedMatchesWereHarder ? "   -> genuinely harder than chance" : "   -> inside the null"));
    Console.WriteLine($"  verdict : {currentSelection.Verdict}");
    Console.WriteLine();

    // ---------------------------------------------------------------- 6. is refusing NoHistory necessary?
    var noHistory = ctx.Where(c => IsValidation(c) && c.WeakestColdStartClass == "NoHistory").ToList();
    var noHistoryRows = new List<string>();
    Console.WriteLine($"NO_HISTORY CHECK on VALIDATION ({noHistory.Count} matches) - is refusing this group necessary?");
    Console.WriteLine($"  {"model",-28}{"LogLoss",12}{"Brier",10}{"RPS",10}{"Accuracy",10}");
    foreach (var m in new[] { ModelId.Simple, ModelId.TeamStrength, ModelId.IndependentPoisson })
    {
        var acc = new MetricAccumulator(ModelIds.Name(m), "NO_HISTORY", "VALIDATION");
        foreach (var c in noHistory) acc.Add(c.Raw.Probabilities[(int)m], c.Raw.Actual);
        Console.WriteLine($"  {ModelIds.Name(m),-28}{acc.LogLoss,12:0.000000}{acc.Brier,10:0.00000}{acc.Rps,10:0.00000}{acc.Accuracy,10:0.0000}");
        noHistoryRows.Add(string.Join(',', "VALIDATION", ModelIds.Name(m), acc.N.ToString(CultureInfo.InvariantCulture),
            F(acc.LogLoss), F(acc.Brier), F(acc.Rps), F(acc.Accuracy)));
    }
    var restOfValidation = ctx.Where(c => IsValidation(c) && c.WeakestColdStartClass != "NoHistory").ToList();
    var restAcc = new MetricAccumulator("INDEPENDENT_POISSON_V2", "NO_HISTORY", "rest of validation");
    foreach (var c in restOfValidation) restAcc.Add(c.Raw.Probabilities[(int)ModelId.IndependentPoisson], c.Raw.Actual);
    Console.WriteLine($"  {"(everything else, Poisson)",-28}{restAcc.LogLoss,12:0.000000}{restAcc.Brier,10:0.00000}{restAcc.Rps,10:0.00000}{restAcc.Accuracy,10:0.0000}");
    noHistoryRows.Add(string.Join(',', "VALIDATION", "INDEPENDENT_POISSON_V2 (rest of validation)",
        restAcc.N.ToString(CultureInfo.InvariantCulture), F(restAcc.LogLoss), F(restAcc.Brier), F(restAcc.Rps), F(restAcc.Accuracy)));
    Console.WriteLine();

    // ---------------------------------------------------------------- 14. TEST, once
    Console.WriteLine("TEST - scored once, for the current policy and the chosen policy only");
    bool IsTest(MatchContext c) => c.Segment == Segment.Test;
    var testRows = new List<PolicyResult>();
    var testSelection = new List<SelectionTest.Result>();
    var testLosses = ctx.Where(IsTest).Select(c => c.ModelLogLoss).ToList();
    var testIdx = Enumerable.Range(0, ctx.Count).Where(i => IsTest(ctx[i])).ToArray();

    Console.WriteLine($"{"policy",-12}{"published",11}{"coverage",10}{"pubLogLoss",12}{"rejLogLoss",12}{"allLogLoss",12}");
    foreach (var policy in new[] { PolicyGrid.Current, chosen }.Distinct())
    {
        var published = decisions[policy.Name];
        var r = PolicyEvaluator.Measure(ctx, published, policy, "TEST", "OVERALL", "all", IsTest);
        testRows.Add(r);
        foreach (var cls in new[] { "NoHistory", "Limited", "Developing", "Established", "Rich" })
            testRows.Add(PolicyEvaluator.Measure(ctx, published, policy, "TEST", "COLD_START_CLASS_WEAKER_SIDE", cls,
                c => IsTest(c) && c.WeakestColdStartClass == cls));
        foreach (var comp in ctx.Where(IsTest).Select(c => c.Raw.CompetitionType).Distinct().OrderBy(x => x, StringComparer.Ordinal))
            testRows.Add(PolicyEvaluator.Measure(ctx, published, policy, "TEST", "COMPETITION_TYPE", comp,
                c => IsTest(c) && c.Raw.CompetitionType == comp));

        testSelection.Add(SelectionTest.Run(testLosses, testIdx.Select(i => published[i]).ToArray(), policy.Name, "TEST"));

        Console.WriteLine($"{policy.Name,-12}{r.Published,11}{r.Coverage * 100,9:0.00}%{r.PublishedLogLoss,12:0.000000}" +
                          $"{r.RejectedLogLoss,12:0.000000}{r.AllLogLoss,12:0.000000}");
    }
    Console.WriteLine();

    // ---------------------------------------------------------------- 8. rejection analysis, current policy
    var currentPublished = decisions[PolicyGrid.Current.Name];
    var rejectionRows = new List<string>();
    var reasonService = new PredictionService(PolicyGrid.Current.Apply(baselineGate));
    var reasonCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    for (var i = 0; i < ctx.Count; i++)
    {
        if (currentPublished[i]) continue;
        var c = ctx[i];
        var dto = reasonService.Build(c.Raw, c.Home, c.Away, c.IdentityConfidence, c.IdentityConfidence,
            c.CompetitionMatchesObserved);
        foreach (var code in dto.GateReason.Split('|')) reasonCounts[code] = reasonCounts.GetValueOrDefault(code) + 1;

        var p = c.Raw.Probabilities[(int)ModelId.IndependentPoisson];
        rejectionRows.Add(string.Join(',',
            Q(c.Raw.MatchId), c.Raw.Date.ToString("yyyy-MM-dd"), c.Segment.ToString().ToUpperInvariant(),
            Q(c.Raw.Season), Q(c.Raw.Competition), Q(c.Raw.CompetitionType), Q(dto.GateReason),
            Q(dto.HomeColdStartClass), Q(dto.AwayColdStartClass), Q(c.WeakestColdStartClass),
            dto.HomePriorMatches.ToString(CultureInfo.InvariantCulture),
            dto.AwayPriorMatches.ToString(CultureInfo.InvariantCulture),
            dto.CompetitionMatchesObserved.ToString(CultureInfo.InvariantCulture),
            F(p.Home), F(p.Draw), F(p.Away), c.Raw.Actual.ToString(), F(c.ModelLogLoss)));
    }
    Console.WriteLine($"REJECTION ANALYSIS under the current policy: {rejectionRows.Count} rows");
    foreach (var kv in reasonCounts.OrderByDescending(k => k.Value))
        Console.WriteLine($"  {kv.Key,-32}{kv.Value,7}");

    Console.WriteLine();

    // ---------------------------------------------------------------- outputs
    WriteLines(Path.Combine(outDir, "gate_policy_comparison.csv"), PolicyResult.CsvHeader,
        comparison.OrderByDescending(r => r.Coverage).ThenBy(r => r.Policy.Name, StringComparer.Ordinal).Select(r => r.ToCsv()));
    WriteLines(Path.Combine(outDir, "gate_policy_validation.csv"), PolicyResult.CsvHeader,
        detailed.Select(r => r.ToCsv()));
    WriteLines(Path.Combine(outDir, "gate_policy_test.csv"), PolicyResult.CsvHeader,
        testRows.Select(r => r.ToCsv()));
    WriteLines(Path.Combine(outDir, "risk_coverage_curve.csv"),
        SelectionTest.Result.CsvHeader,
        selection.OrderByDescending(s => s.Published).Select(s => s.ToCsv())
            .Concat(testSelection.Select(s => s.ToCsv())));
    WriteLines(Path.Combine(outDir, "nohistory_analysis.csv"),
        "Segment,Model,N,LogLoss,Brier,RPS,Accuracy", noHistoryRows);
    WriteLines(Path.Combine(outDir, "gate_rejection_analysis.csv"),
        "MatchId,Date,Segment,Season,Competition,CompetitionType,GateReason," +
        "HomeColdStartClass,AwayColdStartClass,WeakestColdStartClass," +
        "HomePriorMatches,AwayPriorMatches,CompetitionMatchesObserved," +
        "ModelHomeProbability,ModelDrawProbability,ModelAwayProbability,ActualOutcome,ModelLogLoss",
        rejectionRows);

    Console.WriteLine($"elapsed {total.Elapsed.TotalSeconds:0.0} s   outputs written to {outDir}");
    return modelLogLosses.Count == 1 ? 0 : 3;
}

static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);
static string Q(string s) => s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
static string Short(string verdict) => verdict switch
{
    "SELECTS BETTER THAN CHANCE" => "selects > chance",
    "WORSE THAN CHANCE - refusing matches the model handled well" => "WORSE than chance",
    "INDISTINGUISHABLE FROM RANDOM REFUSAL" => "= random",
    _ => "no refusals"
};

static void WriteLines(string path, string header, IEnumerable<string> lines)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine(header);
    foreach (var l in lines) w.WriteLine(l);
}
