using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Dataset;
using Formax.Infrastructure.Historical.Prediction.Selection;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Historical.Prediction.Ensemble;

/// <summary>Ensemble ağırlıklandırma stratejisi.</summary>
public enum EnsembleWeighting
{
    /// <summary>Eşit ağırlık (basit ortalama).</summary>
    Equal,
    /// <summary>Validation log loss'unu en aza indiren deterministik ağırlıklar (coordinate descent).</summary>
    OptimizedOnValidation
}

/// <summary>Bir modelin (bireysel veya ensemble) karşılaştırma satırı.</summary>
public sealed record EnsembleModelComparison
{
    public required string Name { get; init; }
    public required ClassificationMetrics Metrics { get; init; }
    public bool IsEnsemble { get; init; }
    /// <summary>Bu modelin ensemble içindeki ağırlığı (ensemble satırı için 1).</summary>
    public double Weight { get; init; }
}

/// <summary>Ensemble kurulum + karşılaştırma sonucu.</summary>
public sealed record EnsembleBuildResult
{
    public required EnsembleModel Ensemble { get; init; }
    public required IReadOnlyList<EnsembleModelComparison> Comparisons { get; init; }
    public required string RecommendedModel { get; init; }
    public required bool EnsembleIsBest { get; init; }
    public required string Rationale { get; init; }
    public required DatasetMetadata DatasetMetadata { get; init; }
}

/// <summary>Kayıtlı adaylardan ensemble kuran servis sözleşmesi.</summary>
public interface IEnsembleBuilder
{
    Task<EnsembleBuildResult> BuildAsync(EnsembleWeighting weighting = EnsembleWeighting.OptimizedOnValidation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ensemble motoru. TEK kaynak = Dataset v1 (<see cref="IDatasetBuilder"/>). Kayıtlı TÜM adayları (LR, LightGBM,
/// ileride XGBoost/CatBoost) train'de eğitir, validation olasılıklarını önbelleğe alır ve DETERMİNİSTİK
/// coordinate-descent ile log loss'u en aza indiren ağırlıkları bulur. Ensemble ile bireysel modelleri AYNI
/// veri + AYNI hesaplayıcıyla karşılaştırır; kazananı objektif seçer (ensemble daha iyi değilse dürüstçe söyler).
/// </summary>
public sealed class EnsembleBuilder : IEnsembleBuilder
{
    private const int Classes = 3;
    private static readonly double[] WeightGrid = BuildGrid(); // 0.00, 0.05, ... , 1.00

    private readonly IDatasetBuilder _datasetBuilder;
    private readonly IReadOnlyList<IModelCandidate> _candidates;
    private readonly ILogger<EnsembleBuilder> _logger;

    public EnsembleBuilder(IDatasetBuilder datasetBuilder, IEnumerable<IModelCandidate> candidates, ILogger<EnsembleBuilder> logger)
    {
        _datasetBuilder = datasetBuilder;
        _candidates = candidates.ToList();
        _logger = logger;
    }

    public async Task<EnsembleBuildResult> BuildAsync(EnsembleWeighting weighting = EnsembleWeighting.OptimizedOnValidation, CancellationToken cancellationToken = default)
    {
        if (_candidates.Count == 0) throw new InvalidOperationException("Ensemble için model adayı yok.");

        var dataset = await _datasetBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
        if (dataset.Validation.Count == 0) throw new InvalidOperationException("Dataset v1 validation boş — ensemble kurulamaz.");

        // Her adayı train'de eğit (aynı dataset örneği).
        var models = new List<ITrainedModel>(_candidates.Count);
        foreach (var c in _candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            models.Add(c.Train(dataset));
        }

        var valFeatures = dataset.Validation.Select(s => s.Features).ToList();
        var valLabels = dataset.Validation.Select(s => s.Label).ToList();

        // Validation olasılıklarını modeller başına ÖNBELLEĞE al (ağırlık araması hızlı + deterministik).
        var valProbas = models.Select(m => valFeatures.Select(m.PredictProba).ToArray()).ToList();

        var weights = weighting == EnsembleWeighting.Equal
            ? Enumerable.Repeat(1d / models.Count, models.Count).ToArray()
            : OptimizeWeights(valProbas, valLabels);

        var ensemble = new EnsembleModel(models, weights);

        // Bireysel + ensemble metriklerini AYNI validation üzerinde üret.
        var comparisons = new List<EnsembleModelComparison>();
        for (var i = 0; i < models.Count; i++)
        {
            var metrics = ClassificationMetricsCalculator.Evaluate(valProbas[i], valLabels);
            comparisons.Add(new EnsembleModelComparison { Name = models[i].Name, Metrics = metrics, Weight = ensemble.Weights[i] });
            _logger.LogInformation("Ensemble üye: {Name} w={W:0.000} logloss={LL:0.0000} acc={A:P2}", models[i].Name, ensemble.Weights[i], metrics.LogLoss, metrics.Accuracy);
        }

        var ensembleValProbas = Combine(valProbas, weights);
        var ensembleMetrics = ClassificationMetricsCalculator.Evaluate(ensembleValProbas, valLabels);
        comparisons.Add(new EnsembleModelComparison { Name = "Ensemble", Metrics = ensembleMetrics, IsEnsemble = true, Weight = 1d });

        // OBJEKTİF karar: en düşük val log loss.
        var ranked = comparisons.OrderBy(c => c.Metrics.LogLoss).ThenByDescending(c => c.Metrics.MacroF1).ToList();
        var winner = ranked[0];
        var ensembleIsBest = winner.IsEnsemble;
        var rationale = BuildRationale(ranked, ensemble.Weights, models);

        _logger.LogInformation("Ensemble kararı: {Winner} (ensembleIsBest={Best}) — {Why}", winner.Name, ensembleIsBest, rationale);

        return new EnsembleBuildResult
        {
            Ensemble = ensemble,
            Comparisons = ranked,
            RecommendedModel = winner.Name,
            EnsembleIsBest = ensembleIsBest,
            Rationale = rationale,
            DatasetMetadata = dataset.Metadata
        };
    }

    // ── Deterministik ağırlık optimizasyonu (coordinate descent, önbelleklenmiş val olasılıkları) ──
    private static double[] OptimizeWeights(IReadOnlyList<double[][]> valProbas, IReadOnlyList<int> labels)
    {
        var n = valProbas.Count;
        var w = Enumerable.Repeat(1d / n, n).ToArray();
        var best = LogLoss(Combine(valProbas, w), labels);

        for (var round = 0; round < 30; round++)
        {
            var improved = false;
            for (var i = 0; i < n; i++)
            {
                foreach (var cand in WeightGrid)
                {
                    var w2 = SetAndRenormalize(w, i, cand);
                    var loss = LogLoss(Combine(valProbas, w2), labels);
                    if (loss < best - 1e-12)
                    {
                        best = loss;
                        w = w2;
                        improved = true;
                    }
                }
            }
            if (!improved) break;
        }
        return w;
    }

    /// <summary>i. ağırlığı cand yapar, kalan (1-cand) diğerlerine mevcut oranlarınca dağıtılır (simplex adımı).</summary>
    private static double[] SetAndRenormalize(double[] w, int i, double cand)
    {
        var n = w.Length;
        var result = new double[n];
        result[i] = cand;
        var othersSum = 0d;
        for (var k = 0; k < n; k++) if (k != i) othersSum += w[k];

        var remaining = 1d - cand;
        if (othersSum <= 1e-12)
        {
            var share = n > 1 ? remaining / (n - 1) : 0d;
            for (var k = 0; k < n; k++) if (k != i) result[k] = share;
        }
        else
        {
            for (var k = 0; k < n; k++) if (k != i) result[k] = remaining * (w[k] / othersSum);
        }
        return result;
    }

    private static double[][] Combine(IReadOnlyList<double[][]> valProbas, double[] weights)
    {
        var samples = valProbas[0].Length;
        var result = new double[samples][];
        for (var s = 0; s < samples; s++)
        {
            var acc = new double[Classes];
            for (var i = 0; i < valProbas.Count; i++)
            {
                var p = valProbas[i][s];
                for (var c = 0; c < Classes; c++) acc[c] += weights[i] * p[c];
            }
            var sum = acc[0] + acc[1] + acc[2];
            if (sum > 0) for (var c = 0; c < Classes; c++) acc[c] /= sum;
            result[s] = acc;
        }
        return result;
    }

    private static double LogLoss(double[][] probas, IReadOnlyList<int> labels)
    {
        double sum = 0;
        for (var s = 0; s < probas.Length; s++)
            sum += -Math.Log(Math.Clamp(probas[s][labels[s]], 1e-15, 1d));
        return probas.Length == 0 ? 0 : sum / probas.Length;
    }

    private static string BuildRationale(IReadOnlyList<EnsembleModelComparison> ranked, IReadOnlyList<double> weights, IReadOnlyList<ITrainedModel> models)
    {
        var ensemble = ranked.First(c => c.IsEnsemble);
        var bestIndividual = ranked.First(c => !c.IsEnsemble);
        var wStr = string.Join(", ", models.Select((m, i) => $"{m.Name}={weights[i]:0.00}"));

        if (ranked[0].IsEnsemble)
        {
            var delta = bestIndividual.Metrics.LogLoss - ensemble.Metrics.LogLoss;
            return $"Ensemble KAZANDI: en düşük val log loss ({ensemble.Metrics.LogLoss:0.0000}), en iyi bireyselden ({bestIndividual.Name} {bestIndividual.Metrics.LogLoss:0.0000}) {delta:0.0000} daha düşük. " +
                   $"Ağırlıklar [{wStr}]. Modeller birbirini tamamlayarak olasılık kalitesini artırdı.";
        }

        var gap = ensemble.Metrics.LogLoss - bestIndividual.Metrics.LogLoss;
        var dominant = weights.Select((wv, i) => (wv, i)).OrderByDescending(t => t.wv).First();
        return $"Ensemble bireysel en iyiyi YENMEDİ (dürüst rapor). Ensemble log loss={ensemble.Metrics.LogLoss:0.0000}, " +
               $"en iyi bireysel {bestIndividual.Name}={bestIndividual.Metrics.LogLoss:0.0000} (Δ={gap:0.0000} daha kötü). " +
               $"Neden: optimize ağırlıklar [{wStr}] büyük ölçüde {models[dominant.i].Name}'e yığıldı → modeller yeterince tamamlayıcı değil / korele. " +
               $"Öneri: {bestIndividual.Name}.";
    }

    private static double[] BuildGrid()
    {
        var g = new List<double>();
        for (var v = 0; v <= 20; v++) g.Add(v * 0.05);
        return g.ToArray();
    }
}
