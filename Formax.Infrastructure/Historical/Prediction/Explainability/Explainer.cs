using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Formax.Infrastructure.Historical.Prediction.Ensemble;

namespace Formax.Infrastructure.Historical.Prediction.Explainability;

/// <summary>Ensemble tahminlerini açıklayan servis (occlusion / baseline-ablation).</summary>
public interface IExplainer
{
    /// <summary>Tek tahmin için açıklama: top feature'lar + katkılar + insan-okunur metin. Deterministik.</summary>
    PredictionExplanation Explain(IProbabilityEnsemble ensemble, double[] features);

    /// <summary>Bir örneklem üzerinde global feature önemi (ortalama |katkı|). Deterministik.</summary>
    GlobalFeatureImportance ComputeGlobalImportance(IProbabilityEnsemble ensemble, IReadOnlyList<double[]> samples);
}

/// <summary>
/// Occlusion (baseline-ablation) açıklayıcı. Ensemble'a KARA KUTU olarak uygulanır → LR + LightGBM'i birlikte
/// kapsar (Confidence Engine'e dokunmaz). Her feature için: değeri baseline'a (eğitim-ortalaması) çekip ensemble
/// olasılığındaki gerçek değişim ölçülür = feature'ın tahmine katkısı. Rastgelelik yok → deterministik.
/// NOT: ML.NET çok-sınıflı heterojen ensemble için tam TreeSHAP sağlamadığından, en doğru uygulanabilir
/// bilimsel yöntem budur; LR bileşeni için yön olarak tam linear katkıyla örtüşür. Uydurma açıklama yok.
/// </summary>
public sealed class Explainer : IExplainer
{
    private static readonly string[] Outcomes = { "H", "D", "A" };

    private readonly ExplainerModel _model;

    public Explainer(ExplainerModel model) => _model = model;

    public PredictionExplanation Explain(IProbabilityEnsemble ensemble, double[] features)
    {
        var probability = ensemble.PredictProba(features);
        var target = ArgMax(probability);

        var contributions = ComputeContributions(ensemble, features, probability[target], target);
        var top = contributions
            .OrderByDescending(c => Math.Abs(c.Contribution))
            .ThenBy(c => c.Index)
            .Take(_model.TopK)
            .ToList();

        return new PredictionExplanation
        {
            PredictedClass = target,
            PredictedOutcome = Outcomes[target],
            Probability = probability,
            TopFeatures = top,
            Explanation = BuildText(Outcomes[target], probability[target], top),
            Method = _model.Method
        };
    }

    public GlobalFeatureImportance ComputeGlobalImportance(IProbabilityEnsemble ensemble, IReadOnlyList<double[]> samples)
    {
        var f = _model.FeatureNames.Count;
        var sumAbs = new double[f];

        foreach (var x in samples)
        {
            var probability = ensemble.PredictProba(x);
            var target = ArgMax(probability);
            foreach (var c in ComputeContributions(ensemble, x, probability[target], target))
                sumAbs[c.Index] += Math.Abs(c.Contribution);
        }

        var ranking = Enumerable.Range(0, f)
            .Select(j => new FeatureImportanceItem
            {
                Index = j,
                Name = _model.FeatureNames[j],
                MeanAbsContribution = samples.Count == 0 ? 0 : sumAbs[j] / samples.Count
            })
            .OrderByDescending(r => r.MeanAbsContribution)
            .ThenBy(r => r.Index)
            .ToList();

        return new GlobalFeatureImportance { Ranking = ranking, SampleCount = samples.Count, Method = _model.Method };
    }

    private List<FeatureContribution> ComputeContributions(IProbabilityEnsemble ensemble, double[] features, double pTarget, int target)
    {
        var result = new List<FeatureContribution>(features.Length);
        for (var j = 0; j < features.Length; j++)
        {
            var baseline = _model.Baseline.Length > j ? _model.Baseline[j] : 0d;

            var occluded = (double[])features.Clone();
            occluded[j] = baseline;
            var pOccluded = ensemble.PredictProba(occluded);

            result.Add(new FeatureContribution
            {
                Index = j,
                Name = _model.FeatureNames[j],
                Value = features[j],
                Contribution = pTarget - pOccluded[target]
            });
        }
        return result;
    }

    private static int ArgMax(double[] p)
    {
        var best = 0; for (var c = 1; c < p.Length; c++) if (p[c] > p[best]) best = c;
        return best;
    }

    private static string BuildText(string outcome, double probability, IReadOnlyList<FeatureContribution> top)
    {
        var label = outcome switch { "H" => "Ev sahibi galibiyeti", "D" => "Beraberlik", _ => "Deplasman galibiyeti" };
        var sb = new StringBuilder();
        sb.Append($"{label} (olasılık {probability.ToString("P1", CultureInfo.InvariantCulture)}) tahmininin başlıca nedenleri: ");

        var parts = new List<string>();
        foreach (var c in top)
        {
            var dir = c.Contribution >= 0 ? "destek" : "zayıflatıcı";
            var pp = (c.Contribution * 100).ToString("+0.0;-0.0", CultureInfo.InvariantCulture);
            var val = c.Value.ToString("0.###", CultureInfo.InvariantCulture);
            parts.Add($"{c.Name}={val} ({pp} puan, {dir})");
        }
        sb.Append(string.Join("; ", parts));
        sb.Append('.');
        return sb.ToString();
    }
}
