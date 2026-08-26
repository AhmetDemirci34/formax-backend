using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Dataset;

namespace Formax.Infrastructure.Historical.Prediction.Explainability;

/// <summary>Dataset v1'den occlusion baseline'ı (eğitim-ortalaması) üreten servis sözleşmesi.</summary>
public interface IExplainerBuilder
{
    Task<ExplainerModel> BuildAsync(int topK = 8, CancellationToken cancellationToken = default);
}

/// <summary>
/// Explainer artifact'ını Dataset v1'den kurar: baseline = TRAIN split'in feature-başına ortalaması (deterministik).
/// TEK kaynak = Dataset v1 (<see cref="IDatasetBuilder"/>, Feature Store). Historical'a dokunmaz.
/// </summary>
public sealed class ExplainerBuilder : IExplainerBuilder
{
    private readonly IDatasetBuilder _datasetBuilder;

    public ExplainerBuilder(IDatasetBuilder datasetBuilder) => _datasetBuilder = datasetBuilder;

    public async Task<ExplainerModel> BuildAsync(int topK = 8, CancellationToken cancellationToken = default)
    {
        var dataset = await _datasetBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
        var f = dataset.Metadata.FeatureCount;

        var mean = new double[f];
        foreach (var s in dataset.Train)
            for (var j = 0; j < f; j++) mean[j] += s.Features[j];
        if (dataset.Train.Count > 0)
            for (var j = 0; j < f; j++) mean[j] /= dataset.Train.Count;

        return new ExplainerModel
        {
            Method = "occlusion-baseline-ablation",
            TopK = topK,
            Baseline = mean,
            FeatureNames = dataset.Metadata.FeatureNames
        };
    }
}

/// <summary>Explainer artifact'ını kalıcılaştırma sözleşmesi (model artifact'ine açıklama desteği).</summary>
public interface IExplainerStore
{
    Task SaveAsync(ExplainerModel model, string path, CancellationToken cancellationToken = default);
    Task<ExplainerModel?> LoadAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>Dosya tabanlı explainer deposu (deterministik JSON).</summary>
public sealed class FileExplainerStore : IExplainerStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task SaveAsync(ExplainerModel model, string path, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(model, Json), cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExplainerModel?> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ExplainerModel>(json, Json);
    }
}
