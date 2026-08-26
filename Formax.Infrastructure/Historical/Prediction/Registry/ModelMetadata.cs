using System;
using System.Collections.Generic;
using Formax.Infrastructure.Historical.Prediction.Confidence;
using Formax.Infrastructure.Historical.Prediction.Ensemble;
using Formax.Infrastructure.Historical.Prediction.Explainability;

namespace Formax.Infrastructure.Historical.Prediction.Registry;

/// <summary>Bir model versiyonunun hyperparametreleri (tekrar-üretilebilirlik için saklanır).</summary>
public sealed record ModelHyperparameters
{
    public double LrLearningRate { get; init; }
    public int LrEpochs { get; init; }
    public double LrL2 { get; init; }
    public int LgbmIterations { get; init; }
    public double LgbmLearningRate { get; init; }
    public int LgbmLeaves { get; init; }
    public string EnsembleWeighting { get; init; } = "OptimizedOnValidation";
    public IReadOnlyList<double> EnsembleWeights { get; init; } = Array.Empty<double>();
    public double ConfidenceTemperature { get; init; } = 1.0;
}

/// <summary>Bir model versiyonunun validation metrikleri (özet).</summary>
public sealed record ModelMetricsSummary
{
    public double Accuracy { get; init; }
    public double LogLoss { get; init; }
    public double Brier { get; init; }
    public double MacroF1 { get; init; }
    public int ValidationSamples { get; init; }
}

/// <summary>
/// Model versiyonu metadata'sı (serileştirilebilir). Train tarihi, feature şeması, dataset versiyonu,
/// hyperparametreler ve metrikleri saklar. VersionId, modeli tanımlayan alanlardan DETERMİNİSTİK türetilir
/// (aynı config + veri → aynı VersionId → idempotent kayıt).
/// </summary>
public sealed record ModelMetadata
{
    public required string VersionId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime TrainedAtUtc { get; init; }

    public string DatasetVersion { get; init; } = "v1";
    public int TrainSampleCount { get; init; }

    public int FeatureCount { get; init; }
    public IReadOnlyList<string> FeatureSchema { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EnsembleMembers { get; init; } = Array.Empty<string>();
    public ModelHyperparameters Hyperparameters { get; init; } = new();
    public ModelMetricsSummary Metrics { get; init; } = new();
    public string ExplainMethod { get; init; } = "occlusion-baseline-ablation";
}

/// <summary>Kayıt için gereken metadata girdisi (artifact'lardan türetilemeyen alanlar).</summary>
public sealed record ModelRegistrationInfo
{
    public DateTime TrainedAtUtc { get; init; } = DateTime.UtcNow;
    public string DatasetVersion { get; init; } = "v1";
    public int TrainSampleCount { get; init; }
    public ModelHyperparameters Hyperparameters { get; init; } = new();
    public ModelMetricsSummary Metrics { get; init; } = new();
}

/// <summary>Registry'den yüklenmiş, tahmine hazır model (Ensemble + Confidence + Explainer + metadata).</summary>
public sealed class RegisteredModel
{
    public required ModelMetadata Metadata { get; init; }
    public required EnsembleModel Ensemble { get; init; }
    public required ConfidenceEngine ConfidenceEngine { get; init; }
    public required Explainer ExplainerEngine { get; init; }
}
