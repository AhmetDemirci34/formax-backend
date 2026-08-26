using System.Globalization;
using System.Text;
using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.ModelValidation.Services;
using Formax.Prediction.Config;
using Formax.Prediction.Models;
using Formax.Prediction.Services;
using Formax.Prediction.Tests;
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
var baselinePath = ArgValue("--baseline") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "model_validation_v2", "model_comparison.csv");
var outDir = ArgValue("--out") ?? Path.Combine(root, "FORMAX_PROBABILITY_ENGINE", "prediction_gate_v1");
var gateConfigPath = ArgValue("--gate-config") ?? Path.Combine(outDir, "gate.config.json");

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
switch (command)
{
    case "test": return RunTests();
    case "run": return RunAll();
    case "all": { var t = RunTests(); return t != 0 ? t : RunAll(); }
    default:
        Console.WriteLine("FormaxPredictionGate - raw probability -> prediction gate -> prediction DTO (research only)");
        Console.WriteLine("  test   gate, confidence, DTO contract and leakage tests");
        Console.WriteLine("  run    produce the DTO stream, the log, the gate report and the regression proof");
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
    GateTests.Register(runner,
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

    var ts = TeamStrengthConfig.Load(validatedTsPath);
    var dc = DixonColesConfig.Load(dcConfigPath);
    var split = SplitConfig.Load(splitPath);
    var gateCfg = GateConfig.Load(gateConfigPath);

    var read = MatchCsvReader.Read(datasetPath, ts);
    var matches = read.Matches;

    Console.WriteLine("== FORMAX PROBABILITY OUTPUT + PREDICTION GATE V1 ==");
    Console.WriteLine($"model     : {gateCfg.ModelVersion} on {gateCfg.TeamStrengthVersion} - FROZEN, not re-tuned");
    Console.WriteLine($"gate      : {gateCfg.Describe()}");
    Console.WriteLine($"dataset   : {matches.Count} eligible matches " +
                      $"(reader already dropped {read.SkippedIdentity} rows for unconfirmed identity, " +
                      $"{read.SkippedStatus} for match status, {read.SkippedNotEligible} as not model-eligible)");

    // ---------------------------------------------------------------- raw model, untouched
    var built = new TeamStrengthService(ts).Build(matches);
    var snapshots = new Dictionary<(string, string), TeamStrengthSnapshot>(built.Snapshots.Count);
    foreach (var s in built.Snapshots) snapshots[(s.MatchId, s.Side)] = s;

    var rows = new Dictionary<(string matchId, string side), StrengthRow>(built.Snapshots.Count);
    foreach (var s in built.Snapshots) rows[(s.MatchId, s.Side)] = StrengthPipeline.ToRow(s);

    var rawPreds = new List<MatchPrediction>(matches.Count);
    var backtest = new BacktestV2(dc, new ModelParameters { Rho = 0.0, BivariateC = 0.0 }, split)
        .Run(matches, rows, ModelMask.IndependentPoisson, rawPreds.Add);
    Console.WriteLine($"raw       : {backtest.MatchesPredicted} predictions, leakage {backtest.LeakageViolations}, " +
                      $"normalisation {backtest.NormalisationViolations}");

    var coverage = CompetitionCoverage.Build(matches);
    var identity = matches.ToDictionary(m => m.MatchId, m => m.IdentityConfidence, StringComparer.Ordinal);

    // ---------------------------------------------------------------- raw -> gate -> DTO
    var service = new PredictionService(gateCfg);
    var dtos = new List<PredictionDto>(rawPreds.Count);
    foreach (var raw in rawPreds)
    {
        snapshots.TryGetValue((raw.MatchId, "HOME"), out var h);
        snapshots.TryGetValue((raw.MatchId, "AWAY"), out var a);
        var conf = identity.TryGetValue(raw.MatchId, out var c) ? c : "";
        dtos.Add(service.Build(raw, h, a, conf, conf, coverage.For(raw.MatchId)));
    }

    var eligible = dtos.Count(d => d.PredictionEligible);
    Console.WriteLine($"gate      : {eligible} eligible / {dtos.Count} " +
                      $"({100.0 * eligible / dtos.Count:0.00}%), {dtos.Count - eligible} refused");
    Console.WriteLine();

    // ---------------------------------------------------------------- 7. regression against the V2 baseline
    var baseline = ReadBaseline(baselinePath);
    var regression = new List<string>();
    Console.WriteLine("REGRESSION - the output layer must not have changed the model");
    Console.WriteLine($"{"segment",-12}{"N",8}{"LogLoss",12}{"baseline",12}{"Brier",11}{"RPS",11}{"Accuracy",11}  status");
    var regressionOk = true;
    foreach (var seg in new[] { "TRAIN", "VALIDATION", "TEST", "FULL" })
    {
        var slice = seg == "FULL"
            ? dtos.Zip(rawPreds).ToList()
            : dtos.Zip(rawPreds).Where(z => z.Second.Segment.ToString().ToUpperInvariant() == seg).ToList();

        var acc = new MetricAccumulator(gateCfg.ModelVersion, "REGRESSION", seg);
        foreach (var (d, r) in slice)
            acc.Add(new ProbTriple(d.ModelHomeProbability, d.ModelDrawProbability, d.ModelAwayProbability, 1e-15), r.Actual);

        var expected = baseline.TryGetValue(seg, out var b) ? b : (n: -1, ll: double.NaN, br: double.NaN, rps: double.NaN, acc: double.NaN);
        var ok = expected.n == acc.N
              && Math.Abs(expected.ll - acc.LogLoss) < 5e-7
              && Math.Abs(expected.br - acc.Brier) < 5e-7
              && Math.Abs(expected.rps - acc.Rps) < 5e-7
              && Math.Abs(expected.acc - acc.Accuracy) < 5e-7;
        regressionOk &= ok;

        Console.WriteLine($"{seg,-12}{acc.N,8}{acc.LogLoss,12:0.000000}{expected.ll,12:0.000000}" +
                          $"{acc.Brier,11:0.00000}{acc.Rps,11:0.00000}{acc.Accuracy,11:0.0000}  {(ok ? "MATCH" : "DIFFERS")}");
        regression.Add(string.Join(',', seg, acc.N.ToString(CultureInfo.InvariantCulture),
            F(acc.LogLoss), F(expected.ll), F(acc.Brier), F(expected.br), F(acc.Rps), F(expected.rps),
            F(acc.Accuracy), F(expected.acc), ok ? "MATCH" : "DIFFERS"));
    }
    Console.WriteLine();

    // ---------------------------------------------------------------- operational metrics: published only
    Console.WriteLine("PUBLISHED PREDICTIONS ONLY - what the gate actually lets out");
    Console.WriteLine($"{"segment",-12}{"published",11}{"refused",9}{"LogLoss",12}{"vs all",12}");
    var operational = new List<string>();
    foreach (var seg in new[] { "TRAIN", "VALIDATION", "TEST", "FULL" })
    {
        var slice = dtos.Zip(rawPreds)
            .Where(z => seg == "FULL" || z.Second.Segment.ToString().ToUpperInvariant() == seg).ToList();
        var pub = slice.Where(z => z.First.PredictionEligible).ToList();

        var accPub = new MetricAccumulator(gateCfg.ModelVersion, "PUBLISHED", seg);
        foreach (var (d, r) in pub)
            accPub.Add(new ProbTriple(d.HomeProbability!.Value, d.DrawProbability!.Value, d.AwayProbability!.Value, 1e-15), r.Actual);

        var accAll = new MetricAccumulator(gateCfg.ModelVersion, "ALL", seg);
        foreach (var (d, r) in slice)
            accAll.Add(new ProbTriple(d.ModelHomeProbability, d.ModelDrawProbability, d.ModelAwayProbability, 1e-15), r.Actual);

        Console.WriteLine($"{seg,-12}{pub.Count,11}{slice.Count - pub.Count,9}{accPub.LogLoss,12:0.000000}{accAll.LogLoss,12:0.000000}");
        operational.Add(string.Join(',', seg, pub.Count.ToString(CultureInfo.InvariantCulture),
            (slice.Count - pub.Count).ToString(CultureInfo.InvariantCulture),
            F(accPub.LogLoss), F(accPub.Brier), F(accPub.Rps), F(accPub.Accuracy),
            F(accAll.LogLoss), F(accAll.Brier), F(accAll.Rps), F(accAll.Accuracy)));
    }
    Console.WriteLine();

    // ---------------------------------------------------------------- gate breakdown
    var codeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var d in dtos.Where(d => !d.PredictionEligible))
        foreach (var code in d.GateReason.Split('|'))
            codeCounts[code] = codeCounts.GetValueOrDefault(code) + 1;

    Console.WriteLine("GATE REFUSALS by reason (a match can fire more than one)");
    foreach (var kv in codeCounts.OrderByDescending(k => k.Value))
        Console.WriteLine($"  {kv.Key,-32}{kv.Value,7}");
    Console.WriteLine();

    Console.WriteLine("CONFIDENCE distribution of published predictions");
    foreach (var g in dtos.Where(d => d.PredictionEligible).GroupBy(d => d.ConfidenceClass).OrderBy(g => g.Key))
        Console.WriteLine($"  {g.Key.ToString().ToUpperInvariant(),-12}{g.Count(),8}  ({100.0 * g.Count() / eligible:0.00}%)");
    Console.WriteLine();

    // ---------------------------------------------------------------- policy sensitivity (informational)
    // What a stricter history bar would cost. Measured on VALIDATION, never on TEST: the default bar
    // is declared in gate.config.json and was NOT chosen from these numbers - they exist so that
    // changing it later is a decision made with the price on the table.
    Console.WriteLine("POLICY SENSITIVITY on VALIDATION - the cost of a stricter history bar (informational only)");
    Console.WriteLine($"{"minPriorMatches",16}{"published",11}{"coverage",11}{"LogLoss",12}");
    var sensitivity = new List<string>();
    foreach (var bar in new[] { 1, 2, 3, 5, 10 })
    {
        var probe = GateConfig.Load(gateConfigPath);
        probe.MinPriorMatchesPerTeam = bar;
        var probeSvc = new PredictionService(probe);
        var accP = new MetricAccumulator(gateCfg.ModelVersion, "SENSITIVITY", bar.ToString());
        var pub = 0; var tot = 0;
        for (var i = 0; i < rawPreds.Count; i++)
        {
            if (rawPreds[i].Segment != Segment.Validation) continue;
            tot++;
            snapshots.TryGetValue((rawPreds[i].MatchId, "HOME"), out var ph);
            snapshots.TryGetValue((rawPreds[i].MatchId, "AWAY"), out var pa);
            var pconf = identity[rawPreds[i].MatchId];
            var d = probeSvc.Build(rawPreds[i], ph, pa, pconf, pconf, coverage.For(rawPreds[i].MatchId));
            if (!d.PredictionEligible) continue;
            pub++;
            accP.Add(new ProbTriple(d.HomeProbability!.Value, d.DrawProbability!.Value, d.AwayProbability!.Value, 1e-15),
                rawPreds[i].Actual);
        }
        Console.WriteLine($"{bar,16}{pub,11}{100.0 * pub / tot,10:0.00}%{accP.LogLoss,12:0.000000}");
        sensitivity.Add(string.Join(',', bar.ToString(CultureInfo.InvariantCulture),
            pub.ToString(CultureInfo.InvariantCulture), tot.ToString(CultureInfo.InvariantCulture),
            (100.0 * pub / tot).ToString("0.0000", CultureInfo.InvariantCulture),
            F(accP.LogLoss), F(accP.Brier), F(accP.Rps), F(accP.Accuracy)));
    }
    Console.WriteLine();

    // ---------------------------------------------------------------- 4. extreme probabilities are untouched
    var extreme = dtos.Where(d => d.PredictionEligible)
        .Select(d => Math.Max(d.HomeProbability!.Value, Math.Max(d.DrawProbability!.Value, d.AwayProbability!.Value)))
        .ToList();
    var over90 = extreme.Count(p => p >= 0.90);
    var over80 = extreme.Count(p => p >= 0.80);
    var maxPublished = extreme.Count == 0 ? 0 : extreme.Max();
    var maxModel = dtos.Max(d => Math.Max(d.ModelHomeProbability, Math.Max(d.ModelDrawProbability, d.ModelAwayProbability)));
    Console.WriteLine($"EXTREME PROBABILITIES - published unclipped: >=90% {over90}, >=80% {over80}, " +
                      $"highest published {maxPublished:0.0000}, highest the model produced {maxModel:0.0000}");
    Console.WriteLine();

    // ---------------------------------------------------------------- 6. log and settlement
    var log = dtos.Select(PredictionLogRecord.From).ToList();
    var unsettledLog = log.Select(r => r.ToCsv()).ToList();   // captured BEFORE settlement

    var results = rawPreds.ToDictionary(p => p.MatchId,
        p => (p.Actual, p.HomeGoals, p.AwayGoals, p.Date), StringComparer.Ordinal);
    var settlement = PredictionSettlement.Settle(log, results);
    Console.WriteLine($"PREDICTION LOG: {log.Count} rows written, settled {settlement.Settled}, " +
                      $"no result yet {settlement.NoResultYet}, rejected as early {settlement.RejectedAsEarly}");

    var settledPublished = log.Where(r => r.LogLoss.HasValue).ToList();
    var settledMean = settledPublished.Count == 0 ? double.NaN : settledPublished.Average(r => r.LogLoss!.Value);
    Console.WriteLine($"  log loss recomputed from the settled log: {settledMean:0.000000} " +
                      $"over {settledPublished.Count} published+settled rows");
    Console.WriteLine();

    // ---------------------------------------------------------------- audit
    var audit = GateAudit.Run(dtos, rawPreds, gateCfg, regressionOk, settledMean, log);
    foreach (var a in audit) Console.WriteLine($"  {a.status,-6} {a.check}: expected {a.expected}, observed {a.observed}");
    Console.WriteLine();

    // ---------------------------------------------------------------- outputs
    WriteLines(Path.Combine(outDir, "predictions_dto.csv"), PredictionDto.CsvHeader, dtos.Select(d => d.ToCsv()));
    WriteLines(Path.Combine(outDir, "prediction_log.csv"), PredictionLogRecord.CsvHeader, unsettledLog);
    WriteLines(Path.Combine(outDir, "prediction_log_settled.csv"), PredictionLogRecord.CsvHeader, log.Select(r => r.ToCsv()));
    WriteLines(Path.Combine(outDir, "regression_baseline.csv"),
        "Segment,N,LogLoss,BaselineLogLoss,Brier,BaselineBrier,RPS,BaselineRPS,Accuracy,BaselineAccuracy,Status", regression);
    WriteLines(Path.Combine(outDir, "gate_summary.csv"),
        "Segment,Published,Refused,PublishedLogLoss,PublishedBrier,PublishedRPS,PublishedAccuracy," +
        "AllLogLoss,AllBrier,AllRPS,AllAccuracy", operational);
    WriteLines(Path.Combine(outDir, "gate_policy_sensitivity.csv"),
        "MinPriorMatchesPerTeam,Published,Total,CoveragePercent,PublishedLogLoss,PublishedBrier,PublishedRPS,PublishedAccuracy",
        sensitivity);
    WriteLines(Path.Combine(outDir, "gate_reasons.csv"), "GateCode,Matches",
        codeCounts.OrderByDescending(k => k.Value).Select(k => $"{k.Key},{k.Value}"));
    WriteGroups(Path.Combine(outDir, "gate_by_competition.csv"), "CompetitionType", dtos, rawPreds, d => d.CompetitionType);
    WriteGroups(Path.Combine(outDir, "gate_by_cold_start.csv"), "WeakestColdStartClass", dtos, rawPreds,
        d => Weakest(d.HomeColdStartClass, d.AwayColdStartClass));
    WriteLines(Path.Combine(outDir, "gate_audit.csv"), "Check,Expected,Observed,Status",
        audit.Select(a => $"{Csv(a.check)},{Csv(a.expected)},{Csv(a.observed)},{a.status}"));
    if (!File.Exists(gateConfigPath)) WriteGateConfig(gateConfigPath, gateCfg);

    Console.WriteLine($"elapsed {total.Elapsed.TotalSeconds:0.0} s   outputs written to {outDir}");
    return audit.All(a => a.status == "PASS") ? 0 : 3;
}

static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);
static string Csv(string s) => s.Contains(',') ? "\"" + s + "\"" : s;

static string Weakest(string a, string b)
{
    static int Rank(string c) => c switch
    {
        "NoHistory" => 0, "Limited" => 1, "Developing" => 2, "Established" => 3, "Rich" => 4, _ => 5
    };
    return Rank(a) <= Rank(b) ? a : b;
}

static void WriteLines(string path, string header, IEnumerable<string> lines)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine(header);
    foreach (var l in lines) w.WriteLine(l);
}

static void WriteGroups(string path, string groupName, IReadOnlyList<PredictionDto> dtos,
    IReadOnlyList<MatchPrediction> raws, Func<PredictionDto, string> key)
{
    var lines = new List<string>();
    foreach (var seg in new[] { "TRAIN", "VALIDATION", "TEST" })
        foreach (var g in dtos.Zip(raws)
                     .Where(z => z.Second.Segment.ToString().ToUpperInvariant() == seg)
                     .GroupBy(z => key(z.First)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var all = g.ToList();
            var pub = all.Where(z => z.First.PredictionEligible).ToList();

            var accPub = new MetricAccumulator("x", "y", "z");
            foreach (var (d, r) in pub)
                accPub.Add(new ProbTriple(d.HomeProbability!.Value, d.DrawProbability!.Value, d.AwayProbability!.Value, 1e-15), r.Actual);
            var accAll = new MetricAccumulator("x", "y", "z");
            foreach (var (d, r) in all)
                accAll.Add(new ProbTriple(d.ModelHomeProbability, d.ModelDrawProbability, d.ModelAwayProbability, 1e-15), r.Actual);

            lines.Add(string.Join(',', seg, Csv(g.Key), all.Count.ToString(CultureInfo.InvariantCulture),
                pub.Count.ToString(CultureInfo.InvariantCulture),
                (all.Count - pub.Count).ToString(CultureInfo.InvariantCulture),
                (100.0 * pub.Count / all.Count).ToString("0.0000", CultureInfo.InvariantCulture),
                F(accPub.LogLoss), F(accAll.LogLoss)));
        }
    WriteLines(path, $"Segment,{groupName},Matches,Published,Refused,PublishedPercent,PublishedLogLoss,AllLogLoss", lines);
}

static void WriteGateConfig(string path, GateConfig c)
{
    var sb = new StringBuilder();
    sb.AppendLine("{");
    sb.AppendLine("  \"_comment\": \"Prediction gate policy. These are POLICY thresholds, not fitted parameters: the gate decides whether a probability may be published, it never changes one. Nothing here was selected by scoring the test segment.\",");
    sb.AppendLine($"  \"gateVersion\": \"{c.GateVersion}\",");
    sb.AppendLine($"  \"modelVersion\": \"{c.ModelVersion}\",");
    sb.AppendLine($"  \"teamStrengthVersion\": \"{c.TeamStrengthVersion}\",");
    sb.AppendLine($"  \"requiredIdentityConfidence\": \"{c.RequiredIdentityConfidence}\",");
    sb.AppendLine($"  \"minPriorMatchesPerTeam\": {c.MinPriorMatchesPerTeam},");
    sb.AppendLine("  \"_minPriorMatchesPerTeam\": \"A side with zero prior matches is refused: its snapshot IS the prior, so a published number would describe the opponent and the competition, not this team. POLICY, not a fitted value.\",");
    sb.AppendLine($"  \"minEffectiveMatchesPerTeam\": {F(c.MinEffectiveMatchesPerTeam)},");
    sb.AppendLine($"  \"minCompetitionMatchesObserved\": {c.MinCompetitionMatchesObserved},");
    sb.AppendLine("  \"_minCompetitionMatchesObserved\": \"Matches the frozen context's own MinBaselineSamples: below it the goal baseline is a config seed rather than learned data.\",");
    sb.AppendLine($"  \"lambdaMin\": {F(c.LambdaMin)},");
    sb.AppendLine($"  \"lambdaMax\": {F(c.LambdaMax)},");
    sb.AppendLine("  \"_lambdaClamp\": \"The frozen model's own clamp. The gate does not apply it - it only refuses a prediction whose lambda is SITTING on it, because such a value is saturated rather than measured.\",");
    sb.AppendLine($"  \"guardRailTolerance\": {c.GuardRailTolerance.ToString("0.0e+0", CultureInfo.InvariantCulture)},");
    sb.AppendLine($"  \"probabilitySumTolerance\": {c.ProbabilitySumTolerance.ToString("0.0e+0", CultureInfo.InvariantCulture)},");
    sb.AppendLine($"  \"confidenceLowMin\": {c.ConfidenceLowMin},");
    sb.AppendLine($"  \"confidenceMediumLowMin\": {c.ConfidenceMediumLowMin},");
    sb.AppendLine($"  \"confidenceMediumMin\": {c.ConfidenceMediumMin},");
    sb.AppendLine($"  \"confidenceHighMin\": {c.ConfidenceHighMin},");
    sb.AppendLine("  \"_confidence\": \"Prior-match thresholds, set by the WEAKER side. Confidence describes the evidence and never the probability - the classifier's input type carries no probability field at all.\",");
    sb.AppendLine($"  \"maxPriorWeightForFullConfidence\": {F(c.MaxPriorWeightForFullConfidence)}");
    sb.AppendLine("}");
    File.WriteAllText(path, sb.ToString());
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
