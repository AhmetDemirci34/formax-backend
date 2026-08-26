using System.Globalization;
using Formax.Contract.Models;
using Formax.Contract.Services;
using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;
using Formax.Infrastructure.Predictions;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.ModelValidation.Services;
using Formax.Prediction.Config;
using Formax.Prediction.Services;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Services;

namespace Formax.ShadowRunner;

/// <summary>
/// §19 — üretim kod yolunun araştırma referansına karşı regresyonu.
///
/// Üretim servisi ile aynı motor zincirini, aynı yapılandırma dosyalarını okuyarak, YALNIZ
/// araştırma veri seti üzerinde çalıştırır ve <c>model_validation_v2/model_comparison.csv</c>'den
/// OKUNAN referansla karşılaştırır. Sapma 0 olmalıdır.
///
/// Neden bu doğru testtir: üretim, motorun kodunu ProjectReference ile paylaşır. Aynı girdi
/// verildiğinde aynı sayıyı üretmesi gerekir. Canlı gölge koşusu ise motora DAHA GÜNCEL kanıt
/// (veri setinden sonra biten üretim maçları) verir — bu bir model değişikliği değil, aynı modelin
/// güncel veriyle çalışmasıdır ve rating'lerin referans anlık görüntüsünden farklı olması beklenir.
/// Bu yüzden regresyon, girdinin sabit tutulduğu bu koşuyla ölçülür.
/// </summary>
public static class Regression
{
    public static int Run(PredictionEngineOptions options, string engineRoot)
    {
        var baselinePath = Path.Combine(engineRoot, "FORMAX_PROBABILITY_ENGINE", "model_validation_v2", "model_comparison.csv");

        var ts = TeamStrengthConfig.Load(options.ValidatedTeamStrengthConfigPath);
        var dc = DixonColesConfig.Load(options.DixonColesConfigPath);
        var split = SplitConfig.Load(options.SplitConfigPath);
        var gate = GateConfig.Load(options.GateConfigPath);
        var contractService = new ContractService(gate, ContractVersions.Current, options.ValidatedTeamStrengthConfigPath);

        var matches = MatchCsvReader.Read(options.HistoricalDatasetPath, ts).Matches;

        var built = new TeamStrengthService(ts).Build(matches);
        var snapshots = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side));
        var rows = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side), StrengthPipeline.ToRow);
        var coverage = CompetitionCoverage.Build(matches);

        var preds = new List<MatchPrediction>(matches.Count);
        new BacktestV2(dc, new ModelParameters { Rho = 0.0, BivariateC = 0.0 }, split)
            .Run(matches, rows, ModelMask.IndependentPoisson, preds.Add);

        // Üretimin yaptığının aynısı: ham tahmin -> ContractService -> yayımlanan olasılık.
        // Sözleşmenin olasılığı değiştirmediği de burada ölçülür.
        var contractDrift = 0.0;
        var accepted = 0;
        foreach (var raw in preds)
        {
            snapshots.TryGetValue((raw.MatchId, "HOME"), out var h);
            snapshots.TryGetValue((raw.MatchId, "AWAY"), out var a);
            var c = contractService.Build(raw, h, a, "CONFIRMED", "CONFIRMED", coverage.For(raw.MatchId));
            if (!c.PredictionEligible) continue;
            accepted++;
            var p = raw.Probabilities[(int)ModelId.IndependentPoisson];
            contractDrift = Math.Max(contractDrift, Math.Abs(c.HomeProbability!.Value - p.Home));
            contractDrift = Math.Max(contractDrift, Math.Abs(c.DrawProbability!.Value - p.Draw));
            contractDrift = Math.Max(contractDrift, Math.Abs(c.AwayProbability!.Value - p.Away));
        }

        var baseline = ReadBaseline(baselinePath);
        var ok = true;

        Console.WriteLine("== §19 MODEL REGRESSION — production code path vs model_validation_v2 ==");
        Console.WriteLine($"engine config : {options.ValidatedTeamStrengthConfigPath}");
        Console.WriteLine($"fingerprint   : {contractService.ModelFingerprint}");
        Console.WriteLine();
        Console.WriteLine($"{"segment",-12}{"N",8}{"LogLoss",12}{"reference",12}{"deviation",14}  status");

        foreach (var seg in new[] { "TRAIN", "VALIDATION", "TEST", "FULL" })
        {
            var slice = preds.Where(p => seg == "FULL" || p.Segment.ToString().ToUpperInvariant() == seg).ToList();
            var acc = new MetricAccumulator("INDEPENDENT_POISSON_V2", "REGRESSION", seg);
            foreach (var p in slice) acc.Add(p.Probabilities[(int)ModelId.IndependentPoisson], p.Actual);

            var e = baseline.TryGetValue(seg, out var b) ? b : (n: -1, ll: double.NaN);
            var dev = Math.Abs(e.ll - acc.LogLoss);
            var match = e.n == acc.N && dev < 5e-7;
            ok &= match;

            Console.WriteLine($"{seg,-12}{acc.N,8}{acc.LogLoss,12:0.000000}{e.ll,12:0.000000}" +
                              $"{dev,14:0.0e+0}  {(match ? "MATCH" : "FAIL")}");
        }

        Console.WriteLine();
        Console.WriteLine($"contract altered a probability : {contractDrift:0.0e+0} (over {accepted} accepted predictions)");
        ok &= contractDrift == 0.0;

        Console.WriteLine();
        Console.WriteLine(ok ? "REGRESSION: MATCH — production produces the research probabilities exactly"
                             : "REGRESSION: FAIL");
        return ok ? 0 : 3;
    }

    private static Dictionary<string, (int n, double ll)> ReadBaseline(string path)
    {
        var map = new Dictionary<string, (int, double)>(StringComparer.Ordinal);
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
            map[C("Segment")] = (int.TryParse(C("N"), out var nn) ? nn : -1,
                double.TryParse(C("LogLoss"), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN);
        }
        return map;
    }
}
