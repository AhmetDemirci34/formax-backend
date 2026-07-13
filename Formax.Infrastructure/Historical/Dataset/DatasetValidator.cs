using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Infrastructure.Historical.Dataset;

public interface IDatasetValidator
{
    DatasetValidationReport Validate(Dataset dataset);
}

/// <summary>
/// Dataset v1'in üretim hazırlığını doğrular (SALT-OKUNUR — hiçbir şeyi değiştirmez). 10 kontrol,
/// her biri PASS/WARNING/FAIL. Yalnız gerçekten kritik problemler FAIL'dir (leakage, duplicate,
/// NaN/Inf, split çakışması, şema tutarsızlığı). Redundans/multikolinearite gibi durumlar WARNING.
/// </summary>
public sealed class DatasetValidator : IDatasetValidator
{
    public DatasetValidationReport Validate(Dataset ds)
    {
        var f = ds.Metadata.FeatureCount;
        var train = ds.Train; var val = ds.Validation; var test = ds.Test;
        var all = train.Concat(val).Concat(test).ToList();

        var trainIds = train.Select(x => x.MatchId).ToHashSet();
        var valIds = val.Select(x => x.MatchId).ToHashSet();
        var testIds = test.Select(x => x.MatchId).ToHashSet();

        var (mean, std, min, max) = Stats(train, f);
        var (meanTest, _, _, _) = Stats(test, f);

        var checks = new List<ValidationCheck>
        {
            CheckLeakage(train, trainIds, valIds, testIds, f, mean, std),
            CheckMissing(all, f),
            CheckDuplicates(all),
            CheckSeparation(trainIds, valIds, testIds),
            CheckClassBalance(ds),
            CheckFeatureDistribution(ds, std, min, max),
            CheckOutliers(all, f, mean, std),
            CheckBias(mean, meanTest, std, f, ds.Metadata.FeatureNames),
            CheckCorrelation(train, f, mean, std, ds.Metadata.FeatureNames),
            CheckMetadata(ds, f)
        };

        return new DatasetValidationReport { Checks = checks };
    }

    // 1) DATA LEAKAGE: split çakışması + hiçbir feature label'ı neredeyse mükemmel tahmin etmemeli
    private static ValidationCheck CheckLeakage(IReadOnlyList<DatasetSample> train, HashSet<int> tr, HashSet<int> va, HashSet<int> te,
        int f, double[] mean, double[] std)
    {
        var overlap = tr.Count(id => va.Contains(id) || te.Contains(id)) + va.Count(id => te.Contains(id));

        // Her feature'ın 3 one-hot label ile en yüksek |korelasyon|'u
        double maxCorr = 0; var worst = -1;
        for (var c = 0; c < 3; c++)
        {
            var yMean = train.Count(s => s.Label == c) / (double)Math.Max(train.Count, 1);
            var yStd = Math.Sqrt(yMean * (1 - yMean));
            if (yStd < 1e-9) continue;
            for (var j = 0; j < f; j++)
            {
                if (std[j] < 1e-9) continue;
                double cov = 0;
                foreach (var s in train) cov += (s.Features[j] - mean[j]) * ((s.Label == c ? 1d : 0d) - yMean);
                var corr = Math.Abs(cov / (train.Count * std[j] * yStd));
                if (corr > maxCorr) { maxCorr = corr; worst = j; }
            }
        }

        var verdict = (overlap > 0 || maxCorr > 0.95) ? ValidationVerdict.Fail : ValidationVerdict.Pass;
        return new ValidationCheck { Name = "1. Data Leakage", Verdict = verdict,
            Detail = $"split çakışması={overlap}, max feature↔label |corr|={maxCorr:0.000} (feature#{worst})" };
    }

    // 2) MISSING VALUES: NaN/Inf kritik (flattener null→0 impute eder; NaN olmamalı)
    private static ValidationCheck CheckMissing(IReadOnlyList<DatasetSample> all, int f)
    {
        long bad = 0;
        foreach (var s in all)
            for (var j = 0; j < f; j++)
                if (double.IsNaN(s.Features[j]) || double.IsInfinity(s.Features[j])) bad++;
        return new ValidationCheck { Name = "2. Missing Values", Verdict = bad == 0 ? ValidationVerdict.Pass : ValidationVerdict.Fail,
            Detail = bad == 0 ? "NaN/Inf yok (null'lar 0-impute)" : $"{bad} NaN/Inf değer!" };
    }

    // 3) DUPLICATE SAMPLES: aynı matchId birden fazla örnek olmamalı
    private static ValidationCheck CheckDuplicates(IReadOnlyList<DatasetSample> all)
    {
        var ids = all.Select(x => x.MatchId).ToList();
        var dup = ids.Count - ids.Distinct().Count();
        return new ValidationCheck { Name = "3. Duplicate Samples", Verdict = dup == 0 ? ValidationVerdict.Pass : ValidationVerdict.Fail,
            Detail = dup == 0 ? $"{ids.Count} örnek, benzersiz" : $"{dup} duplicate matchId!" };
    }

    // 4) TRAIN/VAL/TEST AYRIMI
    private static ValidationCheck CheckSeparation(HashSet<int> tr, HashSet<int> va, HashSet<int> te)
    {
        var disjoint = !tr.Overlaps(va) && !tr.Overlaps(te) && !va.Overlaps(te);
        return new ValidationCheck { Name = "4. Train/Val/Test Ayrımı", Verdict = disjoint ? ValidationVerdict.Pass : ValidationVerdict.Fail,
            Detail = disjoint ? "tamamen ayrık (kesişim yok)" : "split'ler çakışıyor!" };
    }

    // 5) CLASS BALANCE: hiçbir sınıf çok nadir olmamalı + split'ler arası tutarlı (stratification)
    private static ValidationCheck CheckClassBalance(Dataset ds)
    {
        double[] Frac(SplitClassDistribution d) => d.Total == 0 ? new[] { 0d, 0, 0 }
            : new[] { d.Home / (double)d.Total, d.Draw / (double)d.Total, d.Away / (double)d.Total };
        var tr = Frac(ds.Metadata.TrainDistribution); var te = Frac(ds.Metadata.TestDistribution);
        var minClass = tr.Min();
        var maxDrift = tr.Zip(te, (a, b) => Math.Abs(a - b)).Max();
        var verdict = minClass < 0.05 ? ValidationVerdict.Fail : (minClass < 0.15 || maxDrift > 0.03) ? ValidationVerdict.Warning : ValidationVerdict.Pass;
        return new ValidationCheck { Name = "5. Class Balance", Verdict = verdict,
            Detail = $"train H/D/A={tr[0]:P1}/{tr[1]:P1}/{tr[2]:P1}, min sınıf={minClass:P1}, split-drift={maxDrift:P2}" };
    }

    // 6) FEATURE DISTRIBUTION: sabit (std≈0) feature'lar işe yaramaz → WARNING
    private static ValidationCheck CheckFeatureDistribution(Dataset ds, double[] std, double[] min, double[] max)
    {
        var constant = new List<string>();
        for (var j = 0; j < std.Length; j++)
            if (std[j] < 1e-9) constant.Add(ds.Metadata.FeatureNames[j]);
        var verdict = constant.Count == 0 ? ValidationVerdict.Pass : ValidationVerdict.Warning;
        return new ValidationCheck { Name = "6. Feature Distribution", Verdict = verdict,
            Detail = constant.Count == 0 ? $"{std.Length} feature'ın hepsi değişken (std>0)" : $"sabit feature: {string.Join(", ", constant)}" };
    }

    // 7) OUTLIER: NaN/Inf veya absürt büyük değer kritik; doğal 5σ-aşımı beklenir (kritik değil)
    private static ValidationCheck CheckOutliers(IReadOnlyList<DatasetSample> all, int f, double[] mean, double[] std)
    {
        long extreme = 0; double worstAbs = 0;
        foreach (var s in all)
            for (var j = 0; j < f; j++)
            {
                var v = Math.Abs(s.Features[j]);
                if (v > worstAbs) worstAbs = v;
                if (double.IsNaN(v) || double.IsInfinity(v) || v > 1_000_000) extreme++;
            }
        return new ValidationCheck { Name = "7. Outlier Analysis", Verdict = extreme == 0 ? ValidationVerdict.Pass : ValidationVerdict.Fail,
            Detail = extreme == 0 ? $"kritik outlier yok (max |değer|={worstAbs:0})" : $"{extreme} absürt/Inf değer!" };
    }

    // 8) BIAS: train vs test feature ortalamaları benzer olmalı (covariate shift yok)
    private static ValidationCheck CheckBias(double[] meanTr, double[] meanTe, double[] std, int f, IReadOnlyList<string> names)
    {
        double maxShift = 0; var worst = -1;
        for (var j = 0; j < f; j++)
        {
            if (std[j] < 1e-9) continue;
            var shift = Math.Abs(meanTr[j] - meanTe[j]) / std[j];
            if (shift > maxShift) { maxShift = shift; worst = j; }
        }
        var verdict = maxShift > 0.2 ? ValidationVerdict.Warning : ValidationVerdict.Pass;
        return new ValidationCheck { Name = "8. Bias Analysis", Verdict = verdict,
            Detail = $"max train↔test standart ortalama kayması={maxShift:0.000}" + (worst >= 0 ? $" ({names[worst]})" : "") };
    }

    // 9) FEATURE CORRELATION: yüksek korelasyonlu çiftler (redundans) → WARNING (kritik değil)
    private static ValidationCheck CheckCorrelation(IReadOnlyList<DatasetSample> train, int f, double[] mean, double[] std, IReadOnlyList<string> names)
    {
        // cov ve std AYNI küme (tam train) üzerinden → korelasyon [-1,1] içinde tutarlı.
        var pairs = new List<string>();
        for (var a = 0; a < f; a++)
        {
            if (std[a] < 1e-9) continue;
            for (var b = a + 1; b < f; b++)
            {
                if (std[b] < 1e-9) continue;
                double cov = 0;
                foreach (var s in train) cov += (s.Features[a] - mean[a]) * (s.Features[b] - mean[b]);
                var corr = cov / (train.Count * std[a] * std[b]);
                if (Math.Abs(corr) > 0.9) pairs.Add($"{names[a]}~{names[b]}({corr:0.00})");
            }
        }
        var verdict = pairs.Count == 0 ? ValidationVerdict.Pass : ValidationVerdict.Warning;
        return new ValidationCheck { Name = "9. Feature Correlation", Verdict = verdict,
            Detail = pairs.Count == 0 ? "|corr|>0.95 çift yok" : $"{pairs.Count} redundant çift: {string.Join(", ", pairs.Take(5))}" };
    }

    // 10) METADATA doğrulaması: şema tutarlı mı
    private static ValidationCheck CheckMetadata(Dataset ds, int f)
    {
        var m = ds.Metadata;
        var problems = new List<string>();
        if (m.FeatureNames.Count != f) problems.Add($"FeatureNames({m.FeatureNames.Count})≠FeatureCount({f})");
        if (ds.Train.Count > 0 && ds.Train[0].Features.Length != f) problems.Add("örnek feature uzunluğu≠FeatureCount");
        if (m.TrainCount + m.ValidationCount + m.TestCount != m.TotalSamples) problems.Add("split toplamı≠TotalSamples");
        if (ds.Train.Count != m.TrainCount || ds.Test.Count != m.TestCount) problems.Add("gerçek split sayısı≠metadata");
        return new ValidationCheck { Name = "10. Dataset Metadata", Verdict = problems.Count == 0 ? ValidationVerdict.Pass : ValidationVerdict.Fail,
            Detail = problems.Count == 0 ? $"{f} feature, {m.TotalSamples} örnek, şema tutarlı" : string.Join("; ", problems) };
    }

    private static (double[] mean, double[] std, double[] min, double[] max) Stats(IReadOnlyList<DatasetSample> s, int f)
    {
        var mean = new double[f]; var std = new double[f];
        var min = new double[f]; var max = new double[f];
        for (var j = 0; j < f; j++) { min[j] = double.MaxValue; max[j] = double.MinValue; }
        if (s.Count == 0) return (mean, std, min, max);

        foreach (var x in s)
            for (var j = 0; j < f; j++)
            {
                var v = x.Features[j];
                mean[j] += v;
                if (v < min[j]) min[j] = v;
                if (v > max[j]) max[j] = v;
            }
        for (var j = 0; j < f; j++) mean[j] /= s.Count;

        foreach (var x in s)
            for (var j = 0; j < f; j++) { var d = x.Features[j] - mean[j]; std[j] += d * d; }
        for (var j = 0; j < f; j++) std[j] = Math.Sqrt(std[j] / s.Count);

        return (mean, std, min, max);
    }
}
