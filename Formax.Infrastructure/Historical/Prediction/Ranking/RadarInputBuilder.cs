using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Dataset;
using Formax.Infrastructure.Historical.Features;
using Formax.Infrastructure.Historical.Prediction.Api;

namespace Formax.Infrastructure.Historical.Prediction.Ranking;

/// <summary>Bir maç için <see cref="RadarInput"/> kuran servis (kaynak sorumluluğu burada; motor agnostik).</summary>
public interface IRadarInputBuilder
{
    Task<RadarInput?> BuildAsync(int matchId, CancellationToken cancellationToken = default);
}

/// <summary>
/// RadarInput'u mevcut Probability Engine'den kurar: yapılandırılmış feature vektörü Feature Store'dan
/// (<see cref="IFeatureStoreReader"/>), olasılık + confidence ise aktif Ensemble modelinden
/// (<see cref="IPredictiveModelProvider"/>). Bu kapsam Historical (Feature Store) — canlı fikstür köprüsü YOK.
/// GDP/Probability Engine yapısına DOKUNMAZ; yalnız okur. Deterministik.
/// </summary>
public sealed class RadarInputBuilder : IRadarInputBuilder
{
    private readonly IFeatureStoreReader _featureStore;
    private readonly IPredictiveModelProvider _modelProvider;

    public RadarInputBuilder(IFeatureStoreReader featureStore, IPredictiveModelProvider modelProvider)
    {
        _featureStore = featureStore;
        _modelProvider = modelProvider;
    }

    public async Task<RadarInput?> BuildAsync(int matchId, CancellationToken cancellationToken = default)
    {
        var vector = await _featureStore.ReadAsync(matchId, cancellationToken).ConfigureAwait(false);
        if (vector is null) return null;

        var bundle = await _modelProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var flat = FeatureFlattener.Flatten(vector);
        var (probability, perModel) = bundle.Ensemble.PredictWithComponents(flat);
        var confidence = bundle.ConfidenceEngine.Assess(probability, perModel);

        return new RadarInput
        {
            MatchId = matchId,
            Probability = probability,
            Confidence = confidence,
            Features = vector
        };
    }
}
