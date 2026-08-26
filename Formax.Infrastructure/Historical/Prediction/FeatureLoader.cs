using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Dataset;
using Formax.Infrastructure.Historical.Features;

namespace Formax.Infrastructure.Historical.Prediction;

/// <summary>
/// Probability Engine'in feature yükleme portu. TEK veri kaynağı = Feature Store (<see cref="IFeatureStoreReader"/>);
/// Historical Database'e ASLA doğrudan erişmez. Yapılandırılmış <see cref="MatchFeatureVector"/>'ü
/// prediction-ready <see cref="PredictionFeatureVector"/>'e (sabit-sıralı <c>double[]</c>) çevirir.
/// Salt-okunur → idempotent; çıktı sırası MatchId'ye göre stabil → deterministik.
/// </summary>
public interface IFeatureLoader
{
    /// <summary>Feature dizisinin sabit şema sırası (model kolon hizalaması için tek gerçek kaynak).</summary>
    IReadOnlyList<string> FeatureNames { get; }

    /// <summary>Feature sayısı (her vektörün uzunluğu).</summary>
    int FeatureCount { get; }

    /// <summary>Tek maç için prediction-ready feature vektörü. Feature Store'da yoksa null.</summary>
    Task<PredictionFeatureVector?> LoadAsync(int historicalMatchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Çok sayıda maçı toplu yükler (N+1 yok; reader tek sorgu(lar)la okur). Bulunmayan id'ler atlanır.
    /// Çıktı MatchId'ye göre artan ve tekrarsız → deterministik.
    /// </summary>
    Task<IReadOnlyList<PredictionFeatureVector>> LoadBatchAsync(IReadOnlyCollection<int> historicalMatchIds, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IFeatureLoader"/>
public sealed class FeatureLoader : IFeatureLoader
{
    private readonly IFeatureStoreReader _store;

    public FeatureLoader(IFeatureStoreReader store) => _store = store;

    public IReadOnlyList<string> FeatureNames => FeatureFlattener.FeatureNames;

    public int FeatureCount => FeatureFlattener.Count;

    public async Task<PredictionFeatureVector?> LoadAsync(int historicalMatchId, CancellationToken cancellationToken = default)
    {
        var vector = await _store.ReadAsync(historicalMatchId, cancellationToken).ConfigureAwait(false);
        return vector is null ? null : ToPrediction(vector);
    }

    public async Task<IReadOnlyList<PredictionFeatureVector>> LoadBatchAsync(IReadOnlyCollection<int> historicalMatchIds, CancellationToken cancellationToken = default)
    {
        var vectors = await _store.ReadBatchAsync(historicalMatchIds, cancellationToken).ConfigureAwait(false);

        // Deterministik sıra: girdi sırasından bağımsız, tekrar-üretilebilir.
        return vectors
            .Select(ToPrediction)
            .OrderBy(p => p.MatchId)
            .ToList();
    }

    /// <summary>Yapılandırılmış vektörü sabit-sıralı prediction dizisine düzleştirir (flatten şeması tek kaynak).</summary>
    private static PredictionFeatureVector ToPrediction(MatchFeatureVector vector) => new()
    {
        MatchId = vector.MatchId,
        Features = FeatureFlattener.Flatten(vector)
    };
}
