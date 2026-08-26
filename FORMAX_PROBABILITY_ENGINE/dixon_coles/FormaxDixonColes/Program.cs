using System.Globalization;
using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;
using Formax.DixonColes.Tests;
using Formax.TeamStrength.Tests;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var root = FindRepoRoot(AppContext.BaseDirectory);
var datasetPath  = ArgValue("--dataset")  ?? Path.Combine(root, "FORMAX_HISTORICAL_MASTER", "FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv");
var snapshotPath = ArgValue("--snapshots") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "team_strength", "output", "team_strength_snapshots.csv");
var configPath   = ArgValue("--config")   ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "dixon_coles", "dixoncoles.config.json");
var outDir       = ArgValue("--out")      ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "dixon_coles", "output");

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
switch (command)
{
    case "test": return RunTests();
    case "backtest": return RunBacktest();
    default:
        Console.WriteLine("FormaxDixonColes - baseline probability model + walk-forward backtest (research only)");
        Console.WriteLine("  backtest   replay history and score four models on the same split");
        Console.WriteLine("  test       run the model and leakage test suite");
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
    BacktestTests.Register(runner,
        File.Exists(datasetPath) ? datasetPath : null,
        File.Exists(snapshotPath) ? snapshotPath : null);
    if (!File.Exists(datasetPath) || !File.Exists(snapshotPath))
        Console.WriteLine("  (real-data tests skipped: dataset or snapshots not found)");
    return runner.Run();
}

int RunBacktest()
{
    var cfg = File.Exists(configPath) ? DixonColesConfig.Load(configPath) : new DixonColesConfig();
    if (!File.Exists(configPath)) Console.WriteLine($"WARNING: config not found at {configPath} - defaults in use");
    cfg.Validate();

    Console.WriteLine("== FORMAX DIXON-COLES BASELINE - WALK FORWARD BACKTEST ==");
    Console.WriteLine($"dataset   : {datasetPath}");
    Console.WriteLine($"snapshots : {snapshotPath}");
    Console.WriteLine($"config    : {cfg.Describe()}");
    Console.WriteLine("ALL PARAMETERS ARE UNVALIDATED - nothing has been fitted or tuned on held-out data.");
    Console.WriteLine();

    if (!File.Exists(datasetPath)) { Console.Error.WriteLine("dataset not found"); return 2; }
    if (!File.Exists(snapshotPath)) { Console.Error.WriteLine("team strength snapshots not found - run the team strength engine first"); return 2; }

    var read = Formax.TeamStrength.Services.MatchCsvReader.Read(datasetPath, new Formax.TeamStrength.Config.TeamStrengthConfig());
    var snapshots = StrengthSnapshotReader.Read(snapshotPath);
    Console.WriteLine($"matches   : {read.Matches.Count}");
    Console.WriteLine($"snapshots : {snapshots.Count}");

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var result = new WalkForwardBacktest(cfg).Run(read.Matches, snapshots);
    sw.Stop();

    Console.WriteLine($"predicted : {result.MatchesPredicted}   skipped(no snapshot): {result.MatchesSkippedNoSnapshot}   elapsed: {sw.ElapsedMilliseconds} ms");
    Console.WriteLine($"leakage violations: {result.LeakageViolations}   normalisation violations: {result.NormalisationViolations}");
    Console.WriteLine();

    Directory.CreateDirectory(outDir);
    var models = new[]
    {
        WalkForwardBacktest.SimpleModel,
        WalkForwardBacktest.StrengthModel,
        WalkForwardBacktest.PoissonModel,
        WalkForwardBacktest.DixonColesModelName
    };

    // ---- predictions.csv : the Dixon-Coles predictions, one row per match
    var dcPreds = result.ByModel[WalkForwardBacktest.DixonColesModelName];
    var predPath = Path.Combine(outDir, "predictions.csv");
    using (var w = new StreamWriter(predPath, false))
    {
        w.WriteLine(Prediction.CsvHeader);
        foreach (var p in dcPreds) w.WriteLine(p.ToCsv());
    }

    // ---- overall summary for every model
    var summary = new List<MetricAccumulator>();
    foreach (var mv in models)
    {
        var acc = new MetricAccumulator(mv, "OVERALL", "all");
        foreach (var p in result.ByModel[mv]) acc.Add(p.Probabilities, p.Actual);
        summary.Add(acc);

        // a second cut that ignores the cold-start rows, to show where the number comes from
        var warm = new MetricAccumulator(mv, "OVERALL", "excluding cold start");
        foreach (var p in result.ByModel[mv].Where(p => !p.IsColdStart)) warm.Add(p.Probabilities, p.Actual);
        summary.Add(warm);

        // per season, to see whether the walk-forward warms up
        foreach (var g in result.ByModel[mv].GroupBy(p => p.Season).OrderBy(g => g.Key))
        {
            var a = new MetricAccumulator(mv, "SEASON", g.Key);
            foreach (var p in g) a.Add(p.Probabilities, p.Actual);
            summary.Add(a);
        }
    }
    WriteMetrics(Path.Combine(outDir, "backtest_summary.csv"), summary);

    // ---- per competition type
    var byComp = new List<MetricAccumulator>();
    foreach (var mv in models)
    {
        foreach (var g in result.ByModel[mv].GroupBy(p => p.CompetitionType).OrderBy(g => g.Key))
        {
            var a = new MetricAccumulator(mv, "COMPETITION_TYPE", g.Key);
            foreach (var p in g) a.Add(p.Probabilities, p.Actual);
            byComp.Add(a);
        }
        foreach (var g in result.ByModel[mv].Where(p => p.CompetitionType == "DOMESTIC_LEAGUE")
                     .GroupBy(p => p.Competition).OrderBy(g => g.Key))
        {
            var a = new MetricAccumulator(mv, "COMPETITION", g.Key);
            foreach (var p in g) a.Add(p.Probabilities, p.Actual);
            byComp.Add(a);
        }
    }
    WriteMetrics(Path.Combine(outDir, "metrics_by_competition.csv"), byComp);

    // ---- per cold start class (of the weaker-evidence side) and per confidence
    var byCold = new List<MetricAccumulator>();
    foreach (var mv in models)
    {
        foreach (var g in result.ByModel[mv].GroupBy(p => WeakestClass(p)).OrderBy(g => g.Key))
        {
            var a = new MetricAccumulator(mv, "COLD_START_CLASS_WEAKER_SIDE", g.Key);
            foreach (var p in g) a.Add(p.Probabilities, p.Actual);
            byCold.Add(a);
        }
        foreach (var g in result.ByModel[mv].GroupBy(p => p.IsColdStart ? "cold start (a side has no history)" : "both sides have history"))
        {
            var a = new MetricAccumulator(mv, "COLD_START_FLAG", g.Key);
            foreach (var p in g) a.Add(p.Probabilities, p.Actual);
            byCold.Add(a);
        }
        foreach (var g in result.ByModel[mv].GroupBy(p => UefaOnlyBucket(p)).OrderBy(g => g.Key))
        {
            var a = new MetricAccumulator(mv, "PRIOR_POOL", g.Key);
            foreach (var p in g) a.Add(p.Probabilities, p.Actual);
            byCold.Add(a);
        }
    }
    WriteMetrics(Path.Combine(outDir, "metrics_by_cold_start.csv"), byCold);

    // ---- reliability bands (raw probabilities, no calibration)
    var bands = new List<ReliabilityBand>();
    foreach (var mv in models)
    {
        var edges = new[] { 0.0, 0.10, 0.20, 0.30, 0.40, 0.50, 0.60, 0.70, 0.80, 0.90, 1.0 };
        var set = new List<ReliabilityBand>();
        for (var i = 0; i < edges.Length - 1; i++) set.Add(new ReliabilityBand(mv, edges[i], edges[i + 1]));
        foreach (var p in result.ByModel[mv])
            foreach (var o in new[] { Outcome.HomeWin, Outcome.Draw, Outcome.AwayWin })
            {
                var prob = p.Probabilities[o];
                var band = set.FirstOrDefault(b => b.Contains(prob));
                band?.Add(prob, p.Actual == o);
            }
        bands.AddRange(set);
    }
    using (var w = new StreamWriter(Path.Combine(outDir, "metrics_by_probability_band.csv"), false))
    {
        w.WriteLine(ReliabilityBand.CsvHeader);
        foreach (var b in bands) w.WriteLine(b.ToCsv());
    }

    // ---- console report
    Console.WriteLine("OVERALL (all predictions, raw probabilities, no calibration)");
    Console.WriteLine($"{"model",-28}{"N",8}{"LogLoss",11}{"Brier",10}{"RPS",10}{"Accuracy",10}");
    foreach (var a in summary.Where(a => a.Scope == "OVERALL" && a.Group == "all"))
        Console.WriteLine($"{a.ModelVersion,-28}{a.N,8}{a.LogLoss,11:0.00000}{a.Brier,10:0.00000}{a.Rps,10:0.00000}{a.Accuracy,10:0.0000}");

    Console.WriteLine();
    Console.WriteLine("OVERALL excluding cold-start rows");
    foreach (var a in summary.Where(a => a.Scope == "OVERALL" && a.Group != "all"))
        Console.WriteLine($"{a.ModelVersion,-28}{a.N,8}{a.LogLoss,11:0.00000}{a.Brier,10:0.00000}{a.Rps,10:0.00000}{a.Accuracy,10:0.0000}");

    Console.WriteLine();
    Console.WriteLine("BY COMPETITION TYPE (log loss)");
    var types = byComp.Where(a => a.Scope == "COMPETITION_TYPE").Select(a => a.Group).Distinct().OrderBy(x => x).ToList();
    Console.Write($"{"model",-28}");
    foreach (var ty in types) Console.Write($"{Short(ty),14}");
    Console.WriteLine();
    foreach (var mv in models)
    {
        Console.Write($"{mv,-28}");
        foreach (var ty in types)
        {
            var a = byComp.FirstOrDefault(x => x.ModelVersion == mv && x.Scope == "COMPETITION_TYPE" && x.Group == ty);
            Console.Write(a is null ? $"{"-",14}" : $"{a.LogLoss,14:0.00000}");
        }
        Console.WriteLine();
    }
    Console.Write($"{"(N)",-28}");
    foreach (var ty in types)
    {
        var a = byComp.First(x => x.Scope == "COMPETITION_TYPE" && x.Group == ty);
        Console.Write($"{a.N,14}");
    }
    Console.WriteLine();

    // ---- the verdict the task asks for, computed not asserted
    var dcAcc = summary.First(a => a.ModelVersion == WalkForwardBacktest.DixonColesModelName && a.Group == "all");
    var best = summary.Where(a => a.Group == "all" && a.ModelVersion != WalkForwardBacktest.DixonColesModelName)
                      .OrderBy(a => a.LogLoss).First();
    Console.WriteLine();
    Console.WriteLine($"best reference model by log loss : {best.ModelVersion} ({best.LogLoss:0.00000})");
    Console.WriteLine($"dixon-coles log loss             : {dcAcc.LogLoss:0.00000}");
    var delta = best.LogLoss - dcAcc.LogLoss;
    Console.WriteLine($"improvement over best reference  : {delta:+0.00000;-0.00000;0} log loss");
    Console.WriteLine(delta > 0.005
        ? "VERDICT: Dixon-Coles beats every reference model on this split."
        : "VERDICT: DIXON-COLES IMPROVEMENT NOT PROVEN on this split.");

    Console.WriteLine();
    Console.WriteLine($"written: {predPath}");
    Console.WriteLine($"written: {Path.Combine(outDir, "backtest_summary.csv")}");
    Console.WriteLine($"written: {Path.Combine(outDir, "metrics_by_competition.csv")}");
    Console.WriteLine($"written: {Path.Combine(outDir, "metrics_by_cold_start.csv")}");
    Console.WriteLine($"written: {Path.Combine(outDir, "metrics_by_probability_band.csv")}");

    return (result.LeakageViolations == 0 && result.NormalisationViolations == 0) ? 0 : 3;
}

static string Short(string competitionType) => competitionType switch
{
    "DOMESTIC_LEAGUE" => "DOM_LEAGUE",
    "DOMESTIC_PLAYOFF" => "DOM_PLAYOFF",
    "UEFA_MAIN" => "UEFA_MAIN",
    "UEFA_QUALIFIER" => "UEFA_QUAL",
    "UEFA_QUALIFICATION_PLAYOFF" => "UEFA_QPO",
    _ => competitionType
};

static string WeakestClass(Prediction p)
{
    int Rank(string c) => c switch
    {
        "NoHistory" => 0, "Limited" => 1, "Developing" => 2, "Established" => 3, "Rich" => 4, _ => 5
    };
    return Rank(p.HomeColdStartClass) <= Rank(p.AwayColdStartClass) ? p.HomeColdStartClass : p.AwayColdStartClass;
}

static string UefaOnlyBucket(Prediction p)
{
    var h = p.HomePriorSource.StartsWith("POOL:UEFA", StringComparison.Ordinal);
    var a = p.AwayPriorSource.StartsWith("POOL:UEFA", StringComparison.Ordinal);
    return h && a ? "both sides pooled against UEFA"
         : h || a ? "one side pooled against UEFA"
         : "neither side pooled against UEFA";
}

static void WriteMetrics(string path, IEnumerable<MetricAccumulator> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine(MetricAccumulator.CsvHeader);
    foreach (var r in rows) w.WriteLine(r.ToCsv());
}
