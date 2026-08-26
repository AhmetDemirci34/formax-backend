namespace Formax.Infrastructure.Historical.Prediction;

/// <summary>
/// Probability Engine'e beslenmeye HAZIR feature vektörü: düzleştirilmiş, sabit-sıralı <c>double[]</c>
/// (şema = <see cref="IFeatureLoader.FeatureNames"/> ile birebir aynı sırada). Null'lar 0.0 impute edilir
/// (<see cref="Dataset.FeatureFlattener"/> sözleşmesi). Bu vektör model X girdisidir; target/label İÇERMEZ
/// → leakage yok. Tamamen Feature Store'dan türetilir (Historical Database'e dokunulmaz).
/// </summary>
public sealed record PredictionFeatureVector
{
    /// <summary>Kaynak <c>HistoricalMatch</c> kimliği (Feature Store anahtarı ile aynı).</summary>
    public required int MatchId { get; init; }

    /// <summary>Sabit uzunlukta feature dizisi (<see cref="IFeatureLoader.FeatureCount"/>). Null'lar 0-impute.</summary>
    public required double[] Features { get; init; }
}
