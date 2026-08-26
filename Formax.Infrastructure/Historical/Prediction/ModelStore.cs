using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Historical.Prediction;

/// <summary>
/// Eğitilmiş <see cref="ProbabilityModel"/> artifact'ını kalıcılaştırma sözleşmesi. Model tekrar
/// eğitilebilir olduğu için artifact düz dosya (JSON) olarak saklanır; yükleyip tahmin üretilebilir.
/// (Sürüm/registry yönetimi FAZ 3.8'e bırakıldı — burada tek dosya kaydı yeterli.)
/// </summary>
public interface IModelStore
{
    Task SaveAsync(ProbabilityModel model, string path, CancellationToken cancellationToken = default);
    Task<ProbabilityModel?> LoadAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>Dosya tabanlı model deposu — deterministik JSON (aynı model = aynı içerik).</summary>
public sealed class FileModelStore : IModelStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task SaveAsync(ProbabilityModel model, string path, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(model, Json);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProbabilityModel?> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ProbabilityModel>(json, Json);
    }
}
