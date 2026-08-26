using System.Globalization;
using Formax.Contract.Models;
using Formax.Contract.Services;
using Formax.Contract.Tests;
using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;
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
var baselinePath = ArgValue("--baseline") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "model_validation_v2", "model_comparison.csv");
var outDir = ArgValue("--out") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "prediction_contract_v1");

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
switch (command)
{
    case "test": return RunTests();
    case "run": return RunAll();
    case "all": { var t = RunTests(); return t != 0 ? t : RunAll(); }
    default:
        Console.WriteLine("FormaxPredictionContract - the final prediction contract (preview mode, no production DB)");
        Console.WriteLine("  test   contract, immutability, settlement, versioning and gate-integration tests");
        Console.WriteLine("  run    produce the contract stream, the log, the regression proof and the readiness verdict");
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
    ContractTests.Register(runner,
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

    var versions = ContractVersions.Current;
    var ts = TeamStrengthConfig.Load(validatedTsPath);
    var dc = DixonColesConfig.Load(dcConfigPath);
    var split = SplitConfig.Load(splitPath);
    var gateCfg = GateConfig.Load(gateConfigPath);
    var matches = MatchCsvReader.Read(datasetPath, ts).Matches;

    var service = new ContractService(gateCfg, versions, validatedTsPath);

    Console.WriteLine("== FORMAX FINAL PREDICTION CONTRACT V1 ==");
    Console.WriteLine($"versions        : {versions.Describe()}");
    Console.WriteLine($"model fingerprint: {service.ModelFingerprint}  (sha256 of the validated config, taken at start-up)");
    Console.WriteLine($"runtime         : no LLM, no backtest, no config optimisation - the model is read and used");
    Console.WriteLine($"matches         : {matches.Count}");

    // ---------------------------------------------------------------- the frozen model, read not tuned
    var built = new TeamStrengthService(ts).Build(matches);
    var snapshots = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side));
    var rows = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side), StrengthPipeline.ToRow);

    var rawPreds = new List<MatchPrediction>(matches.Count);
    var backtest = new BacktestV2(dc, new ModelParameters { Rho = 0.0, BivariateC = 0.0 }, split)
        .Run(matches, rows, ModelMask.IndependentPoisson, rawPreds.Add);

    var coverage = CompetitionCoverage.Build(matches);
    var identity = matches.ToDictionary(m => m.MatchId, m => m.IdentityConfidence, StringComparer.Ordinal);
    Console.WriteLine($"raw             : {backtest.MatchesPredicted} predictions, leakage {backtest.LeakageViolations}, " +
                      $"normalisation {backtest.NormalisationViolations}");

    // ---------------------------------------------------------------- raw -> gate -> contract -> store
    var store = new PredictionStore();
    var contracts = new List<PredictionContract>(rawPreds.Count);
    var outcomes = new Dictionary<PublishOutcome, int>();

    foreach (var raw in rawPreds)
    {
        snapshots.TryGetValue((raw.MatchId, "HOME"), out var h);
        snapshots.TryGetValue((raw.MatchId, "AWAY"), out var a);
        var conf = identity[raw.MatchId];
        var contract = service.Build(raw, h, a, conf, conf, coverage.For(raw.MatchId));
        contracts.Add(contract);
        var outcome = store.Publish(contract);
        outcomes[outcome] = outcomes.GetValueOrDefault(outcome) + 1;
    }

    var accepted = contracts.Count(c => c.GateStatus == GateStatus.Accepted);
    var rejected = contracts.Count - accepted;
    Console.WriteLine($"contract        : {accepted} accepted / {rejected} rejected  " +
                      $"({100.0 * accepted / contracts.Count:0.00}% accepted)");
    Console.WriteLine($"store           : {string.Join(", ", outcomes.Select(kv => $"{kv.Key}={kv.Value}"))}");
    Console.WriteLine();

    // ---------------------------------------------------------------- 10. immutability, probed live
    var sample = contracts.First(c => c.PredictionEligible);
    var idempotent = store.Publish(sample);                               // identical -> no-op
    var tampered = sample with { HomeProbability = sample.HomeProbability!.Value + 0.05 };
    var rewrite = store.Publish(tampered);                                // same id, new numbers -> refused

    Console.WriteLine("IMMUTABILITY PROBE");
    Console.WriteLine($"  republish identical  -> {idempotent}");
    Console.WriteLine($"  rewrite probabilities-> {rewrite}");
    Console.WriteLine($"  stored value after the attempt: {store.Get(sample.PredictionId)!.Prediction.HomeProbability:0.00000000} " +
                      $"(attempted {tampered.HomeProbability:0.00000000})");
    Console.WriteLine();

    // ---------------------------------------------------------------- 16. regression
    var baseline = ReadBaseline(baselinePath);
    var regression = new List<string>();
    var regressionOk = true;
    Console.WriteLine("REGRESSION against model_validation_v2 - any deviation is a FAIL");
    Console.WriteLine($"{"segment",-12}{"N",8}{"LogLoss",12}{"baseline",12}{"Brier",11}{"RPS",11}{"Accuracy",11}  status");
    foreach (var seg in new[] { "TRAIN", "VALIDATION", "TEST", "FULL" })
    {
        var slice = rawPreds.Where(r => seg == "FULL" || r.Segment.ToString().ToUpperInvariant() == seg).ToList();
        var acc = new MetricAccumulator(versions.ModelVersion, "REGRESSION", seg);
        foreach (var r in slice) acc.Add(r.Probabilities[(int)ModelId.IndependentPoisson], r.Actual);

        var e = baseline.TryGetValue(seg, out var b) ? b : (n: -1, ll: double.NaN, br: double.NaN, rps: double.NaN, acc: double.NaN);
        var ok = e.n == acc.N && Math.Abs(e.ll - acc.LogLoss) < 5e-7 && Math.Abs(e.br - acc.Brier) < 5e-7
              && Math.Abs(e.rps - acc.Rps) < 5e-7 && Math.Abs(e.acc - acc.Accuracy) < 5e-7;
        regressionOk &= ok;

        Console.WriteLine($"{seg,-12}{acc.N,8}{acc.LogLoss,12:0.000000}{e.ll,12:0.000000}" +
                          $"{acc.Brier,11:0.00000}{acc.Rps,11:0.00000}{acc.Accuracy,11:0.0000}  {(ok ? "MATCH" : "DIFFERS")}");
        regression.Add(string.Join(',', seg, acc.N.ToString(CultureInfo.InvariantCulture),
            F(acc.LogLoss), F(e.ll), F(acc.Brier), F(e.br), F(acc.Rps), F(e.rps), F(acc.Accuracy), F(e.acc),
            ok ? "MATCH" : "DIFFERS"));
    }
    Console.WriteLine();

    // ---------------------------------------------------------------- 11. settlement
    var results = rawPreds.ToDictionary(r => r.MatchId, r => (r.HomeGoals, r.AwayGoals, r.Date), StringComparer.Ordinal);
    var hashesBefore = store.All.ToDictionary(r => r.Prediction.PredictionId, r => r.Prediction.ContentHash, StringComparer.Ordinal);
    var settlementTimestamp = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    var settleReport = store.Settle(results, settlementTimestamp);
    var changedBySettlement = store.All.Count(r => hashesBefore[r.Prediction.PredictionId] != r.Prediction.ContentHash);

    Console.WriteLine($"SETTLEMENT: settled {settleReport.Settled}, already settled {settleReport.AlreadySettled}, " +
                      $"no result yet {settleReport.NoResultYet}, rejected as early {settleReport.RejectedAsEarly}");
    Console.WriteLine($"  predictions changed by settlement: {changedBySettlement}   tampered rows: {store.TamperedRows()}");

    var settled = store.All.Where(r => r.LogLoss.HasValue).ToList();
    var settledLogLoss = settled.Count == 0 ? double.NaN : settled.Average(r => r.LogLoss!.Value);

    var contractAcc = new MetricAccumulator(versions.ModelVersion, "CONTRACT", "accepted");
    var byMatch = rawPreds.ToDictionary(r => r.MatchId, StringComparer.Ordinal);
    foreach (var c in contracts.Where(c => c.PredictionEligible))
        contractAcc.Add(new ProbTriple(c.HomeProbability!.Value, c.DrawProbability!.Value, c.AwayProbability!.Value, 1e-15),
            byMatch[c.MatchId].Actual);

    Console.WriteLine($"  log loss from the settled store : {settledLogLoss:0.000000} over {settled.Count} rows");
    Console.WriteLine($"  log loss from the contract      : {contractAcc.LogLoss:0.000000} over {contractAcc.N} rows");
    Console.WriteLine();

    // ---------------------------------------------------------------- 12. metrics available from the log
    Console.WriteLine("METRICS COMPUTABLE FROM THE PRODUCTION LOG (no optimisation performed)");
    Console.WriteLine($"  LogLoss {contractAcc.LogLoss:0.000000}   Brier {contractAcc.Brier:0.00000}   " +
                      $"RPS {contractAcc.Rps:0.00000}   Accuracy {contractAcc.Accuracy:0.0000}");
    Console.WriteLine();

    // ---------------------------------------------------------------- audit and verdict
    var audit = ContractAudit.Run(contracts, rawPreds, store, regressionOk, settledLogLoss, contractAcc.LogLoss,
        rewrite, idempotent);
    foreach (var a in audit) Console.WriteLine($"  {a.status,-6} {a.check}: expected {a.expected}, observed {a.observed}");
    Console.WriteLine();

    var eligible = contracts.Where(c => c.PredictionEligible).ToList();
    var minProb = eligible.SelectMany(c => new[] { c.HomeProbability!.Value, c.DrawProbability!.Value, c.AwayProbability!.Value }).Min();
    var maxProb = eligible.SelectMany(c => new[] { c.HomeProbability!.Value, c.DrawProbability!.Value, c.AwayProbability!.Value }).Max();
    var maxSumErr = eligible.Max(c => Math.Abs(c.ProbabilitySum - 1.0));
    var leakage = contracts.Count(c => c.EvidenceCutoff.HasValue && c.EvidenceCutoff.Value >= c.MatchDate)
                + backtest.LeakageViolations;
    var ready = audit.All(a => a.status == "PASS");

    Console.WriteLine("==================== FINAL REPORT ====================");
    Console.WriteLine($"ModelVersion        : {versions.ModelVersion}");
    Console.WriteLine($"TeamStrengthVersion : {versions.TeamStrengthVersion}");
    Console.WriteLine($"GateVersion         : {versions.GateVersion}");
    Console.WriteLine($"CalibrationVersion  : {versions.CalibrationVersion}");
    Console.WriteLine();
    Console.WriteLine($"Accepted count      : {accepted}");
    Console.WriteLine($"Rejected count      : {rejected}");
    Console.WriteLine();
    Console.WriteLine($"Probability min/max : {minProb:0.00000000} / {maxProb:0.00000000}");
    Console.WriteLine($"Probability sum err : {maxSumErr:0.0e+0}");
    Console.WriteLine();
    Console.WriteLine($"Regression result   : {(regressionOk ? "MATCH on all four segments" : "DIFFERS - FAIL")}");
    Console.WriteLine($"Leakage result      : {leakage} violations");
    Console.WriteLine();
    Console.WriteLine(ready && regressionOk && leakage == 0
        ? "FINAL PREDICTION CONTRACT READY"
        : "NOT READY");
    Console.WriteLine("======================================================");
    Console.WriteLine();

    // ---------------------------------------------------------------- outputs
    WriteLines(Path.Combine(outDir, "prediction_contract.csv"), PredictionContract.CsvHeader, contracts.Select(c => c.ToCsv()));
    WriteLines(Path.Combine(outDir, "prediction_log.csv"), PredictionRecord.CsvHeader,
        store.All.OrderBy(r => r.Prediction.MatchDate.DayNumber)
                 .ThenBy(r => r.Prediction.MatchId, StringComparer.Ordinal).Select(r => r.ToCsv()));
    WriteLines(Path.Combine(outDir, "regression.csv"),
        "Segment,N,LogLoss,BaselineLogLoss,Brier,BaselineBrier,RPS,BaselineRPS,Accuracy,BaselineAccuracy,Status", regression);
    WriteLines(Path.Combine(outDir, "contract_audit.csv"), "Check,Expected,Observed,Status",
        audit.Select(a => $"{Csv(a.check)},{Csv(a.expected)},{Csv(a.observed)},{a.status}"));
    WriteLines(Path.Combine(outDir, "contract_versions.csv"),
        "Field,Value,Source",
        new[]
        {
            $"ModelVersion,{versions.ModelVersion},frozen",
            $"TeamStrengthVersion,{versions.TeamStrengthVersion},{Csv(validatedTsPath.Replace('\\', '/'))}",
            $"GateVersion,{versions.GateVersion},{Csv(gateConfigPath.Replace('\\', '/'))}",
            $"CalibrationVersion,{versions.CalibrationVersion},calibration_v1 verdict CALIBRATION_IMPROVEMENT_NOT_PROVEN",
            $"ModelFingerprint,{service.ModelFingerprint},sha256 of the validated team strength config",
            $"SplitVersion,{split.SplitVersion},{Csv(splitPath.Replace('\\', '/'))}"
        });

    Console.WriteLine($"elapsed {total.Elapsed.TotalSeconds:0.0} s   outputs written to {outDir}");
    return ready && regressionOk && leakage == 0 ? 0 : 3;
}

static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);
static string Csv(string s) => s.Contains(',') ? "\"" + s + "\"" : s;

static void WriteLines(string path, string header, IEnumerable<string> lines)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine(header);
    foreach (var l in lines) w.WriteLine(l);
}

static Dictionary<string, (int n, double ll, double br, double rps, double acc)> ReadBaseline(string path)
{
    var map = new Dictionary<string, (int, double, double, double, double)>(StringComparer.Ordinal);
    if (!File.Exists(path)) return map;

    using var sr = new StreamReader(path);
    var header = MatchCsvReader.ParseLine(sr.ReadLine() ?? "");
    if (header.Count > 0) header[0] = header[0].TrimStart('﻿');
    var ix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < header.Count; i++) ix[header[i]] = i;

    while (sr.ReadLine() is { } line)
    {
        if (line.Length == 0) continue;
        var f = MatchCsvReader.ParseLine(line);
        string C(string n) => ix.TryGetValue(n, out var i) && i < f.Count ? f[i] : "";
        if (C("ParameterSet") != "VALIDATED_V2" || C("Scope") != "OVERALL" || C("Group") != "all") continue;
        if (C("ModelVersion") != "INDEPENDENT_POISSON_V2") continue;
        double D(string n) => double.TryParse(C(n), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;
        map[C("Segment")] = (int.TryParse(C("N"), out var nn) ? nn : -1, D("LogLoss"), D("Brier"), D("RPS"), D("Accuracy"));
    }
    return map;
}
