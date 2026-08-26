using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Prediction.Confidence;
using Formax.Infrastructure.Historical.Prediction.Ensemble;
using Formax.Infrastructure.Historical.Prediction.Explainability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Historical.Prediction.Registry;

/// <summary>Model versiyonlarını yöneten registry sözleşmesi (kayıt, listeleme, aktif model, yükleme, rollback).</summary>
public interface IModelRegistry
{
    Task<ModelMetadata> RegisterAsync(EnsembleModel ensemble, ConfidenceCalibration calibration, ExplainerModel explainer, ModelRegistrationInfo info, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelMetadata>> ListAsync(CancellationToken cancellationToken = default);
    Task<ModelMetadata?> GetMetadataAsync(string versionId, CancellationToken cancellationToken = default);
    Task<string?> GetActiveVersionAsync(CancellationToken cancellationToken = default);
    Task<ModelMetadata?> GetActiveMetadataAsync(CancellationToken cancellationToken = default);
    /// <summary>Aktif modeli değiştirir (rollback = eski bir versiyonu aktif yapmak). Versiyon yoksa <see cref="KeyNotFoundException"/>.</summary>
    Task SetActiveAsync(string versionId, CancellationToken cancellationToken = default);
    Task<RegisteredModel> LoadAsync(string versionId, CancellationToken cancellationToken = default);
    Task<RegisteredModel?> LoadActiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Dosya tabanlı Model Registry. Her versiyon bir dizin: ensemble/ + confidence.json + explainer.json +
/// metadata.json. Kök dizinde registry.json (versiyon listesi + aktif versiyon). Mevcut store'ları
/// (<see cref="IEnsembleStore"/>, <see cref="IConfidenceCalibrationStore"/>, <see cref="IExplainerStore"/>)
/// yeniden kullanır (yeni mimari yok). VersionId deterministik (içerik-tanımlayıcı) → idempotent kayıt.
/// Scoped store'lar için bir scope açar (singleton captive-dependency yok).
/// </summary>
public sealed class FileModelRegistry : IModelRegistry, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FileModelRegistry> _logger;
    private readonly string _root;
    private readonly SemaphoreSlim _indexGate = new(1, 1);

    public FileModelRegistry(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<FileModelRegistry> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _root = configuration["ProbabilityEngine:ModelRegistryPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "pe-models");
        Directory.CreateDirectory(_root);
    }

    private string VersionDir(string versionId) => Path.Combine(_root, versionId);
    private string IndexPath => Path.Combine(_root, "registry.json");

    public async Task<ModelMetadata> RegisterAsync(EnsembleModel ensemble, ConfidenceCalibration calibration, ExplainerModel explainer, ModelRegistrationInfo info, CancellationToken cancellationToken = default)
    {
        var versionId = ComputeVersionId(ensemble, calibration, explainer, info);
        var dir = VersionDir(versionId);

        var metadata = new ModelMetadata
        {
            VersionId = versionId,
            CreatedAtUtc = DateTime.UtcNow,
            TrainedAtUtc = info.TrainedAtUtc,
            DatasetVersion = info.DatasetVersion,
            TrainSampleCount = info.TrainSampleCount,
            FeatureCount = explainer.Baseline.Length,
            FeatureSchema = explainer.FeatureNames,
            EnsembleMembers = ensemble.ModelNames,
            Hyperparameters = info.Hyperparameters with { EnsembleWeights = ensemble.Weights, ConfidenceTemperature = calibration.Temperature },
            Metrics = info.Metrics,
            ExplainMethod = explainer.Method
        };

        // İçerik zaten kayıtlıysa idempotent: yalnız index'te olduğundan emin ol.
        if (!File.Exists(Path.Combine(dir, "metadata.json")))
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            await sp.GetRequiredService<IEnsembleStore>().SaveAsync(ensemble, Path.Combine(dir, "ensemble"), cancellationToken).ConfigureAwait(false);
            await sp.GetRequiredService<IConfidenceCalibrationStore>().SaveAsync(calibration, Path.Combine(dir, "confidence.json"), cancellationToken).ConfigureAwait(false);
            await sp.GetRequiredService<IExplainerStore>().SaveAsync(explainer, Path.Combine(dir, "explainer.json"), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(dir, "metadata.json"), JsonSerializer.Serialize(metadata, Json), cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Model registry: yeni versiyon kaydedildi {Version}", versionId);
        }

        await UpdateIndexAsync(idx => { if (!idx.Versions.Contains(versionId)) idx.Versions.Add(versionId); }, cancellationToken).ConfigureAwait(false);
        return metadata;
    }

    public async Task<IReadOnlyList<ModelMetadata>> ListAsync(CancellationToken cancellationToken = default)
    {
        var idx = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<ModelMetadata>(idx.Versions.Count);
        foreach (var v in idx.Versions)
        {
            var m = await GetMetadataAsync(v, cancellationToken).ConfigureAwait(false);
            if (m is not null) result.Add(m);
        }
        return result;
    }

    public async Task<ModelMetadata?> GetMetadataAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(VersionDir(versionId), "metadata.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ModelMetadata>(json, Json);
    }

    public async Task<string?> GetActiveVersionAsync(CancellationToken cancellationToken = default)
        => (await ReadIndexAsync(cancellationToken).ConfigureAwait(false)).ActiveVersionId;

    public async Task<ModelMetadata?> GetActiveMetadataAsync(CancellationToken cancellationToken = default)
    {
        var active = await GetActiveVersionAsync(cancellationToken).ConfigureAwait(false);
        return active is null ? null : await GetMetadataAsync(active, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetActiveAsync(string versionId, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path.Combine(VersionDir(versionId), "metadata.json")))
            throw new KeyNotFoundException($"Model versiyonu bulunamadı: {versionId}");
        await UpdateIndexAsync(idx =>
        {
            if (!idx.Versions.Contains(versionId)) idx.Versions.Add(versionId);
            idx.ActiveVersionId = versionId;
        }, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Model registry: aktif model → {Version}", versionId);
    }

    public async Task<RegisteredModel> LoadAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(versionId, cancellationToken).ConfigureAwait(false)
                       ?? throw new KeyNotFoundException($"Model versiyonu bulunamadı: {versionId}");
        var dir = VersionDir(versionId);

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var ensemble = await sp.GetRequiredService<IEnsembleStore>().LoadAsync(Path.Combine(dir, "ensemble"), cancellationToken).ConfigureAwait(false);
        var calibration = await sp.GetRequiredService<IConfidenceCalibrationStore>().LoadAsync(Path.Combine(dir, "confidence.json"), cancellationToken).ConfigureAwait(false)
                          ?? throw new InvalidOperationException($"Confidence artifact eksik: {versionId}");
        var explainer = await sp.GetRequiredService<IExplainerStore>().LoadAsync(Path.Combine(dir, "explainer.json"), cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"Explainer artifact eksik: {versionId}");

        return new RegisteredModel
        {
            Metadata = metadata,
            Ensemble = ensemble,
            ConfidenceEngine = new ConfidenceEngine(calibration),
            ExplainerEngine = new Explainer(explainer)
        };
    }

    public async Task<RegisteredModel?> LoadActiveAsync(CancellationToken cancellationToken = default)
    {
        var active = await GetActiveVersionAsync(cancellationToken).ConfigureAwait(false);
        return active is null ? null : await LoadAsync(active, cancellationToken).ConfigureAwait(false);
    }

    // ── Index (registry.json) — thread-safe read-modify-write ──

    private async Task<RegistryIndex> ReadIndexAsync(CancellationToken ct)
    {
        if (!File.Exists(IndexPath)) return new RegistryIndex();
        var json = await File.ReadAllTextAsync(IndexPath, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<RegistryIndex>(json, Json) ?? new RegistryIndex();
    }

    private async Task UpdateIndexAsync(Action<RegistryIndex> mutate, CancellationToken ct)
    {
        await _indexGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var idx = await ReadIndexAsync(ct).ConfigureAwait(false);
            mutate(idx);
            await File.WriteAllTextAsync(IndexPath, JsonSerializer.Serialize(idx, Json), ct).ConfigureAwait(false);
        }
        finally { _indexGate.Release(); }
    }

    /// <summary>Modeli tanımlayan alanlardan deterministik VersionId (aynı config+veri → aynı id).</summary>
    private static string ComputeVersionId(EnsembleModel ensemble, ConfidenceCalibration calibration, ExplainerModel explainer, ModelRegistrationInfo info)
    {
        var h = info.Hyperparameters;
        var sb = new StringBuilder();
        sb.Append("ds=").Append(info.DatasetVersion).Append('|');
        sb.Append("n=").Append(info.TrainSampleCount).Append('|');
        sb.Append("f=").Append(explainer.Baseline.Length).Append('|');
        sb.Append("names=").Append(string.Join(",", explainer.FeatureNames)).Append('|');
        sb.Append("ens=").Append(string.Join(",", ensemble.ModelNames)).Append('|');
        sb.Append("w=").Append(string.Join(",", ensemble.Weights.Select(x => x.ToString("0.000000")))).Append('|');
        sb.Append("lr=").Append(h.LrLearningRate).Append(',').Append(h.LrEpochs).Append(',').Append(h.LrL2).Append('|');
        sb.Append("lgbm=").Append(h.LgbmIterations).Append(',').Append(h.LgbmLearningRate).Append(',').Append(h.LgbmLeaves).Append('|');
        sb.Append("weighting=").Append(h.EnsembleWeighting).Append('|');
        sb.Append("temp=").Append(calibration.Temperature.ToString("0.000000")).Append('|');
        sb.Append("explain=").Append(explainer.Method);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return "pe-" + Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    public void Dispose() => _indexGate.Dispose();

    private sealed class RegistryIndex
    {
        public string? ActiveVersionId { get; set; }
        public List<string> Versions { get; set; } = new();
    }
}
