using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Dataset;
using Formax.Infrastructure.Historical.Prediction.Confidence;
using Formax.Infrastructure.Historical.Prediction.Ensemble;
using Formax.Infrastructure.Historical.Prediction.Explainability;
using Formax.Infrastructure.Historical.Prediction.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Historical.Prediction.Api;

/// <summary>
/// Eğitilmiş Probability Engine paketi: Ensemble + Confidence + Explainer + hazır motorlar. Değişmez → paylaşımlı,
/// thread-safe. ModelVersion = registry versiyon kimliği (Prediction API'nin kullandığı model).
/// </summary>
public sealed class PredictiveModelBundle
{
    public required EnsembleModel Ensemble { get; init; }
    public required ConfidenceEngine ConfidenceEngine { get; init; }
    public required Explainer ExplainerEngine { get; init; }
    public required string ModelVersion { get; init; }
    public DateTime BuiltAtUtc { get; init; }
}

/// <summary>Eğitilmiş model paketini sağlayan servis (registry'deki AKTİF modeli sunar).</summary>
public interface IPredictiveModelProvider
{
    Task<PredictiveModelBundle> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Prediction API'nin kullandığı model paketini sağlar: registry'deki AKTİF versiyonu yükler ve önbelleğe alır.
/// Aktif model yoksa bir kez eğitir → registry'ye kaydeder → aktif yapar. Aktif versiyon değiştiğinde (switch/
/// rollback) bir sonraki istekte otomatik yeniden yükler → API her zaman aktif modeli kullanır. TEK kaynak =
/// Dataset v1. Singleton; scoped bağımlılıkları scope açarak çözer. Deterministik.
/// </summary>
public sealed class PredictiveModelProvider : IPredictiveModelProvider, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IModelRegistry _registry;
    private readonly ILogger<PredictiveModelProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile PredictiveModelBundle? _cached;

    public PredictiveModelProvider(IServiceScopeFactory scopeFactory, IModelRegistry registry, ILogger<PredictiveModelProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _logger = logger;
    }

    public async Task<PredictiveModelBundle> GetAsync(CancellationToken cancellationToken = default)
    {
        var activeId = await _registry.GetActiveVersionAsync(cancellationToken).ConfigureAwait(false);
        var cached = _cached;
        if (cached is not null && cached.ModelVersion == activeId) return cached;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            activeId = await _registry.GetActiveVersionAsync(cancellationToken).ConfigureAwait(false);
            if (_cached is not null && _cached.ModelVersion == activeId) return _cached;

            RegisteredModel model;
            if (activeId is null)
            {
                // Hiç model yok → eğit + kaydet + aktifle.
                var metadata = await TrainAndRegisterAsync(cancellationToken).ConfigureAwait(false);
                await _registry.SetActiveAsync(metadata.VersionId, cancellationToken).ConfigureAwait(false);
                model = await _registry.LoadAsync(metadata.VersionId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                model = await _registry.LoadAsync(activeId, cancellationToken).ConfigureAwait(false);
            }

            _cached = ToBundle(model);
            _logger.LogInformation("Prediction API aktif model: {Version}", _cached.ModelVersion);
            return _cached;
        }
        finally { _gate.Release(); }
    }

    private static PredictiveModelBundle ToBundle(RegisteredModel model) => new()
    {
        Ensemble = model.Ensemble,
        ConfidenceEngine = model.ConfidenceEngine,
        ExplainerEngine = model.ExplainerEngine,
        ModelVersion = model.Metadata.VersionId,
        BuiltAtUtc = model.Metadata.TrainedAtUtc
    };

    private async Task<ModelMetadata> TrainAndRegisterAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Aktif model yok — Probability Engine eğitiliyor ve registry'ye kaydediliyor...");
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var datasetBuilder = sp.GetRequiredService<IDatasetBuilder>();
        var ensembleBuilder = sp.GetRequiredService<IEnsembleBuilder>();
        var calibrator = sp.GetRequiredService<IConfidenceCalibrator>();
        var explainerBuilder = sp.GetRequiredService<IExplainerBuilder>();

        var ensembleResult = await ensembleBuilder.BuildAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var ensemble = ensembleResult.Ensemble;
        var dataset = await datasetBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);

        var valE = new List<double[]>(dataset.Validation.Count);
        var valPer = new List<double[][]>(dataset.Validation.Count);
        var valY = new List<int>(dataset.Validation.Count);
        foreach (var s in dataset.Validation)
        {
            var (combined, perModel) = ensemble.PredictWithComponents(s.Features);
            valE.Add(combined); valPer.Add(perModel); valY.Add(s.Label);
        }
        var calibration = calibrator.Fit(valE, valPer, valY);
        var explainerModel = await explainerBuilder.BuildAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var ensembleMetrics = ensembleResult.Comparisons.First(c => c.IsEnsemble).Metrics;
        var opt = ModelTrainingOptions.Default;
        var info = new ModelRegistrationInfo
        {
            TrainedAtUtc = DateTime.UtcNow,
            DatasetVersion = dataset.Metadata.Version,
            TrainSampleCount = dataset.Metadata.TrainCount,
            Hyperparameters = new ModelHyperparameters
            {
                LrLearningRate = opt.LearningRate,
                LrEpochs = opt.Epochs,
                LrL2 = opt.L2Regularization,
                LgbmIterations = 100,
                LgbmLearningRate = 0.1,
                LgbmLeaves = 31,
                EnsembleWeighting = nameof(EnsembleWeighting.OptimizedOnValidation)
            },
            Metrics = new ModelMetricsSummary
            {
                Accuracy = ensembleMetrics.Accuracy,
                LogLoss = ensembleMetrics.LogLoss,
                Brier = ensembleMetrics.Brier,
                MacroF1 = ensembleMetrics.MacroF1,
                ValidationSamples = ensembleMetrics.Samples
            }
        };

        return await _registry.RegisterAsync(ensemble, calibration, explainerModel, info, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _gate.Dispose();
}
