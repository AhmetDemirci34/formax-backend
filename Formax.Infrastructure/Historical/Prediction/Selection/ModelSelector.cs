using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Dataset;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Historical.Prediction.Selection;

/// <summary>
/// Model Selection motoru. TEK veri kaynağı = Dataset v1 (<see cref="IDatasetBuilder"/>, yalnız Feature
/// Store). Tüm adayları AYNI dataset örneği üzerinde eğitir, AYNI hesaplayıcı ile validation metriklerini
/// üretir ve OBJEKTİF kazananı seçer (birincil kriter = validation log loss; Probability Engine'in amacı
/// olasılık kalitesidir). Adaylar DI'dan gelir → yeni model eklemek = yeni IModelCandidate kaydetmek.
/// </summary>
public sealed class ModelSelector : IModelSelector
{
    private readonly IDatasetBuilder _datasetBuilder;
    private readonly IReadOnlyList<IModelCandidate> _candidates;
    private readonly ILogger<ModelSelector> _logger;

    /// <summary>Karşılaştırmaya dahil EDİLMEYEN, gerekçesiyle (şeffaflık — fabrikasyon yok).</summary>
    private static readonly string[] Excluded =
    {
        "XGBoost — .NET'te üretim-uygun eğitim yok (bakımsız/yalnız-inference sarmalayıcılar); bu ortamda gerçekten eğitilemez.",
        "CatBoost — .NET eğitimi birinci sınıf desteklenmez (C API / Python trainer); gerçek eğitim doğrulanamaz."
    };

    public ModelSelector(IDatasetBuilder datasetBuilder, IEnumerable<IModelCandidate> candidates, ILogger<ModelSelector> logger)
    {
        _datasetBuilder = datasetBuilder;
        _candidates = candidates.ToList();
        _logger = logger;
    }

    public async Task<ModelSelectionReport> SelectAsync(CancellationToken cancellationToken = default)
    {
        if (_candidates.Count == 0)
            throw new InvalidOperationException("Karşılaştırılacak model adayı yok (IModelCandidate kayıtlı değil).");

        // ── TEK dataset örneği, TÜM adaylara verilir (adil + tek kaynak). ──
        var dataset = await _datasetBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
        if (dataset.Validation.Count == 0)
            throw new InvalidOperationException("Dataset v1 validation boş — model karşılaştırılamaz.");

        var valFeatures = dataset.Validation.Select(s => s.Features).ToList();
        var valLabels = dataset.Validation.Select(s => s.Label).ToList();

        var evaluations = new List<ModelEvaluation>(_candidates.Count);
        foreach (var candidate in _candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Eğitim süresi
            var swTrain = Stopwatch.StartNew();
            var trained = candidate.Train(dataset);
            swTrain.Stop();

            // Tahmin süresi (validation üzerinde) + olasılıklar
            var probas = new List<double[]>(valFeatures.Count);
            var swPredict = Stopwatch.StartNew();
            foreach (var fv in valFeatures) probas.Add(trained.PredictProba(fv));
            swPredict.Stop();

            var metrics = ClassificationMetricsCalculator.Evaluate(probas, valLabels);
            var perThousand = valFeatures.Count == 0 ? 0 : swPredict.Elapsed.TotalMilliseconds / valFeatures.Count * 1000d;

            evaluations.Add(new ModelEvaluation
            {
                ModelName = candidate.Name,
                Metrics = metrics,
                TrainingTimeMs = swTrain.ElapsedMilliseconds,
                PredictionMsPer1k = perThousand,
                ModelSizeBytes = trained.ModelSizeBytes,
                SelectionScore = metrics.LogLoss // birincil objektif kriter (düşük = iyi)
            });

            _logger.LogInformation(
                "Model karşılaştırma: {Name} — acc={Acc:P2}, f1={F1:0.000}, logloss={LL:0.0000}, brier={B:0.0000}, train={T}ms, pred={P:0.00}ms/1k, size={S}B",
                candidate.Name, metrics.Accuracy, metrics.MacroF1, metrics.LogLoss, metrics.Brier,
                swTrain.ElapsedMilliseconds, perThousand, trained.ModelSizeBytes);
        }

        // ── OBJEKTİF kazanan: en düşük validation log loss (eşitlikte daha yüksek makro-F1). ──
        var ranked = evaluations
            .OrderBy(e => e.SelectionScore)
            .ThenByDescending(e => e.Metrics.MacroF1)
            .ToList();
        var winner = ranked[0];
        var runnerUp = ranked.Count > 1 ? ranked[1] : null;

        var rationale = BuildRationale(winner, runnerUp);
        _logger.LogInformation("Recommended Model: {Winner} — {Why}", winner.ModelName, rationale);

        return new ModelSelectionReport
        {
            Evaluations = ranked,
            RecommendedModel = winner.ModelName,
            Rationale = rationale,
            DatasetMetadata = dataset.Metadata,
            ExcludedModels = Excluded
        };
    }

    private static string BuildRationale(ModelEvaluation winner, ModelEvaluation? runnerUp)
    {
        var basis = $"En düşük validation log loss ({winner.Metrics.LogLoss:0.0000}) — Probability Engine'in birincil kriteri olasılık kalitesi. " +
                    $"Doğruluk={winner.Metrics.Accuracy:P2}, makro-F1={winner.Metrics.MacroF1:0.000}, Brier={winner.Metrics.Brier:0.0000}.";
        if (runnerUp is null) return basis + " (Tek karşılaştırılabilir aday.)";
        var delta = runnerUp.Metrics.LogLoss - winner.Metrics.LogLoss;
        return basis + $" İkinci ({runnerUp.ModelName}) log loss'undan {delta:0.0000} daha düşük; " +
               $"eğitim {winner.TrainingTimeMs}ms, tahmin {winner.PredictionMsPer1k:0.0}ms/1k, boyut {winner.ModelSizeBytes}B.";
    }
}
