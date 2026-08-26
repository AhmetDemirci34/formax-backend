using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Historical.Prediction.Confidence;

/// <summary>
/// Confidence kalibrasyon artifact'ını kalıcılaştırma sözleşmesi. Ensemble artifact'ıyla AYNI dizine yazılarak
/// modele confidence desteği eklenir (kaydet/yükle → aynı confidence).
/// </summary>
public interface IConfidenceCalibrationStore
{
    Task SaveAsync(ConfidenceCalibration calibration, string path, CancellationToken cancellationToken = default);
    Task<ConfidenceCalibration?> LoadAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>Dosya tabanlı confidence kalibrasyon deposu (deterministik JSON).</summary>
public sealed class FileConfidenceCalibrationStore : IConfidenceCalibrationStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task SaveAsync(ConfidenceCalibration calibration, string path, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(calibration, Json), cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConfidenceCalibration?> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ConfidenceCalibration>(json, Json);
    }
}
