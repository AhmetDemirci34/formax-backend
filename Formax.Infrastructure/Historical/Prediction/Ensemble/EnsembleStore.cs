using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Prediction.Selection;

namespace Formax.Infrastructure.Historical.Prediction.Ensemble;

/// <summary>
/// Ensemble modelini kalıcılaştırma sözleşmesi. Alt-modeller heterojen olduğu için bir DİZİN olarak saklanır:
/// her alt-model kendi artifact'ıyla (<see cref="ITrainedModel.Serialize"/>) + ağırlık/isim manifesti. Yükleme,
/// kayıtlı adaylar (<see cref="IModelCandidate.Load"/>) ile alt-modelleri isimden yeniden kurar.
/// </summary>
public interface IEnsembleStore
{
    Task SaveAsync(EnsembleModel ensemble, string directory, CancellationToken cancellationToken = default);
    Task<EnsembleModel> LoadAsync(string directory, CancellationToken cancellationToken = default);
}

/// <summary>Dizin tabanlı ensemble deposu (manifest + alt-model artifact'ları).</summary>
public sealed class FileEnsembleStore : IEnsembleStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly IReadOnlyDictionary<string, IModelCandidate> _candidatesByName;

    public FileEnsembleStore(IEnumerable<IModelCandidate> candidates)
        => _candidatesByName = candidates.ToDictionary(c => c.Name);

    public async Task SaveAsync(EnsembleModel ensemble, string directory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var models = ensemble.Models;
        var entries = new List<ManifestEntry>(models.Count);

        for (var i = 0; i < models.Count; i++)
        {
            var file = $"model_{i}_{models[i].Name}.bin";
            await File.WriteAllBytesAsync(Path.Combine(directory, file), models[i].Serialize(), cancellationToken).ConfigureAwait(false);
            entries.Add(new ManifestEntry { Name = models[i].Name, Weight = ensemble.Weights[i], File = file });
        }

        var manifest = new EnsembleManifest { Version = "v1", Entries = entries };
        await File.WriteAllTextAsync(Path.Combine(directory, "ensemble.json"), JsonSerializer.Serialize(manifest, Json), cancellationToken).ConfigureAwait(false);
    }

    public async Task<EnsembleModel> LoadAsync(string directory, CancellationToken cancellationToken = default)
    {
        var manifestJson = await File.ReadAllTextAsync(Path.Combine(directory, "ensemble.json"), cancellationToken).ConfigureAwait(false);
        var manifest = JsonSerializer.Deserialize<EnsembleManifest>(manifestJson, Json)
                       ?? throw new System.InvalidOperationException("Ensemble manifesti okunamadı.");

        var models = new List<ITrainedModel>(manifest.Entries.Count);
        var weights = new List<double>(manifest.Entries.Count);
        foreach (var e in manifest.Entries)
        {
            if (!_candidatesByName.TryGetValue(e.Name, out var candidate))
                throw new System.InvalidOperationException($"'{e.Name}' modeli yüklenemedi — kayıtlı IModelCandidate yok.");
            var bytes = await File.ReadAllBytesAsync(Path.Combine(directory, e.File), cancellationToken).ConfigureAwait(false);
            models.Add(candidate.Load(bytes));
            weights.Add(e.Weight);
        }

        return new EnsembleModel(models, weights.ToArray());
    }

    private sealed class EnsembleManifest
    {
        public string Version { get; set; } = "v1";
        public List<ManifestEntry> Entries { get; set; } = new();
    }

    private sealed class ManifestEntry
    {
        public string Name { get; set; } = string.Empty;
        public double Weight { get; set; }
        public string File { get; set; } = string.Empty;
    }
}
