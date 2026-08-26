using System.Globalization;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;
using Formax.TeamStrength.Services;
using Formax.TeamStrength.Tests;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var root = FindRepoRoot(AppContext.BaseDirectory);
var defaultDataset = Path.Combine(root, "FORMAX_HISTORICAL_MASTER", "FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv");
var defaultConfig  = Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "team_strength", "teamstrength.config.json");
var defaultOutDir  = Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "team_strength", "output");

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var datasetPath = ArgValue("--dataset") ?? defaultDataset;
var configPath  = ArgValue("--config") ?? defaultConfig;
var outDir      = ArgValue("--out") ?? defaultOutDir;

switch (command)
{
    case "test": return RunTests();
    case "build": return RunBuild();
    default:
        Console.WriteLine("FormaxTeamStrength - dynamic team strength engine (design phase tool)");
        Console.WriteLine("  build   produce leakage-safe pre-match snapshots for every match");
        Console.WriteLine("  test    run the engine test suite");
        Console.WriteLine("  options --dataset <csv> --config <json> --out <dir>");
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
    TeamStrengthTests.Register(runner, File.Exists(datasetPath) ? datasetPath : null);
    if (!File.Exists(datasetPath))
        Console.WriteLine($"  (real-data tests skipped: dataset not found at {datasetPath})");
    return runner.Run();
}

int RunBuild()
{
    var cfg = File.Exists(configPath) ? TeamStrengthConfig.Load(configPath) : new TeamStrengthConfig();
    if (!File.Exists(configPath))
        Console.WriteLine($"WARNING: config not found at {configPath} - built-in defaults are in use");
    cfg.Validate();

    Console.WriteLine("== FORMAX TEAM STRENGTH ENGINE ==");
    Console.WriteLine($"dataset : {datasetPath}");
    Console.WriteLine($"config  : {cfg.Describe()}");

    if (!File.Exists(datasetPath)) { Console.Error.WriteLine("dataset not found"); return 2; }

    var read = MatchCsvReader.Read(datasetPath, cfg);
    Console.WriteLine($"rows read           : {read.TotalRows}");
    Console.WriteLine($"  skipped not eligible : {read.SkippedNotEligible}");
    Console.WriteLine($"  skipped identity     : {read.SkippedIdentity}");
    Console.WriteLine($"  skipped status       : {read.SkippedStatus}");
    Console.WriteLine($"  skipped bad score    : {read.SkippedMissingScore}");
    Console.WriteLine($"matches into engine : {read.Matches.Count}");

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var svc = new TeamStrengthService(cfg);
    var result = svc.Build(read.Matches);
    sw.Stop();

    Directory.CreateDirectory(outDir);
    var snapPath = Path.Combine(outDir, "team_strength_snapshots.csv");
    using (var w = new StreamWriter(snapPath, false))
    {
        w.WriteLine(TeamStrengthSnapshot.CsvHeader);
        foreach (var s in result.Snapshots) w.WriteLine(s.ToCsv());
    }

    // final rating per team, useful for eyeballing the result
    var latest = result.Snapshots
        .GroupBy(s => s.TeamId)
        .Select(g => g.OrderBy(s => s.MatchDate.DayNumber).Last())
        .OrderByDescending(s => s.OverallStrength)
        .ToList();
    var latestPath = Path.Combine(outDir, "team_strength_latest.csv");
    using (var w = new StreamWriter(latestPath, false))
    {
        w.WriteLine(TeamStrengthSnapshot.CsvHeader);
        foreach (var s in latest) w.WriteLine(s.ToCsv());
    }

    Console.WriteLine($"matches processed   : {result.MatchesProcessed}");
    Console.WriteLine($"teams processed     : {result.TeamsProcessed}");
    Console.WriteLine($"snapshots produced  : {result.Snapshots.Count}");
    Console.WriteLine($"elapsed             : {sw.ElapsedMilliseconds} ms");
    Console.WriteLine();
    Console.WriteLine("cold start distribution:");
    foreach (var kv in result.ColdStartDistribution.OrderBy(k => k.Key))
        Console.WriteLine($"   {kv.Key,-12} {kv.Value,7}  ({100.0 * kv.Value / result.Snapshots.Count:0.00}%)");
    Console.WriteLine("confidence distribution:");
    foreach (var g in result.Snapshots.GroupBy(s => s.Confidence).OrderBy(g => g.Key))
        Console.WriteLine($"   {g.Key,-12} {g.Count(),7}  ({100.0 * g.Count() / result.Snapshots.Count:0.00}%)");
    Console.WriteLine("prior source distribution:");
    foreach (var kv in result.PriorSourceDistribution.OrderByDescending(k => k.Value))
        Console.WriteLine($"   {kv.Key,-34} {kv.Value,7}");

    // self-audit: the leakage rule must hold on the produced file
    var violations = result.Snapshots.Count(s => s.LastMatchDate.HasValue && s.LastMatchDate.Value >= s.MatchDate);
    var noPrior = result.Snapshots.Count(s => string.IsNullOrWhiteSpace(s.PriorSource));
    var invalid = result.Snapshots.Count(s => double.IsNaN(s.OverallStrength) || double.IsInfinity(s.OverallStrength)
                                              || s.AttackStrength <= 0 || s.DefenseStrength <= 0);
    Console.WriteLine();
    Console.WriteLine("self-audit:");
    Console.WriteLine($"   snapshots using a match dated >= own match date : {violations}");
    Console.WriteLine($"   snapshots without a recorded prior source       : {noPrior}");
    Console.WriteLine($"   snapshots with NaN/infinite/non-positive index  : {invalid}");
    Console.WriteLine();
    Console.WriteLine($"written: {snapPath}");
    Console.WriteLine($"written: {latestPath}");

    return (violations == 0 && noPrior == 0 && invalid == 0) ? 0 : 3;
}
