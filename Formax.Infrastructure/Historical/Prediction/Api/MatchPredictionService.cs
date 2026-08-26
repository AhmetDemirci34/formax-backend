using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Historical.Prediction.Api;

/// <summary>MatchId'den birleşik tahmin (Probability + Confidence + Explainability) üreten servis.</summary>
public interface IMatchPredictionService
{
    /// <summary>Verilen maç için birleşik tahmin. Feature Store'da yoksa null (→ 404). Deterministik (zaman damgası hariç).</summary>
    Task<MatchPredictionResponse?> PredictAsync(int matchId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Probability Engine'in dış dünyaya açılan servis katmanı. Feature'ı Feature Store'dan (<see cref="IFeatureLoader"/>)
/// okur, eğitilmiş paketi (<see cref="IPredictiveModelProvider"/>) kullanarak Ensemble olasılığı + Confidence +
/// Explainability üretip TEK response'ta birleştirir. Clean Architecture: orkestrasyon burada, model detayları
/// alt katmanlarda. Historical DB'ye dokunmaz (yalnız Feature Store).
/// </summary>
public sealed class MatchPredictionService : IMatchPredictionService
{
    private static readonly string[] Outcomes = { "H", "D", "A" };

    private readonly IFeatureLoader _featureLoader;
    private readonly IPredictiveModelProvider _modelProvider;

    public MatchPredictionService(IFeatureLoader featureLoader, IPredictiveModelProvider modelProvider)
    {
        _featureLoader = featureLoader;
        _modelProvider = modelProvider;
    }

    public async Task<MatchPredictionResponse?> PredictAsync(int matchId, CancellationToken cancellationToken = default)
    {
        if (matchId <= 0)
            throw new ArgumentOutOfRangeException(nameof(matchId), "MatchId pozitif bir tam sayı olmalıdır.");

        var featureVector = await _featureLoader.LoadAsync(matchId, cancellationToken).ConfigureAwait(false);
        if (featureVector is null) return null; // Feature Store'da yok → 404

        var bundle = await _modelProvider.GetAsync(cancellationToken).ConfigureAwait(false);

        var (probability, perModel) = bundle.Ensemble.PredictWithComponents(featureVector.Features);
        var confidence = bundle.ConfidenceEngine.Assess(probability, perModel);
        var explanation = bundle.ExplainerEngine.Explain(bundle.Ensemble, featureVector.Features);

        var predicted = ArgMax(probability);

        return new MatchPredictionResponse
        {
            MatchId = matchId,
            PredictedOutcome = Outcomes[predicted],
            HomeWinProbability = probability[0],
            DrawProbability = probability[1],
            AwayWinProbability = probability[2],
            ConfidenceScore = confidence.ConfidenceScore,
            ConfidenceLevel = confidence.Level.ToString(),
            TopFeatures = explanation.TopFeatures
                .Select(f => new FeatureContributionDto { Feature = f.Name, Value = f.Value, Contribution = f.Contribution })
                .ToList(),
            Explanation = explanation.Explanation,
            ModelVersion = bundle.ModelVersion,
            PredictionTimestamp = DateTime.UtcNow
        };
    }

    private static int ArgMax(double[] p)
    {
        var best = 0; for (var c = 1; c < p.Length; c++) if (p[c] > p[best]) best = c;
        return best;
    }
}
