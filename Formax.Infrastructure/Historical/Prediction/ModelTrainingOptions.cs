namespace Formax.Infrastructure.Historical.Prediction;

/// <summary>
/// Model eğitimi hyperparameter altyapısı. TÜM eğitim davranışı buradan kontrol edilir → eğitim
/// deterministik ve tekrar-üretilebilir (aynı options + aynı Dataset v1 = aynı model). Rastgelelik yok;
/// ağırlıklar sıfırdan başlar, full-batch gradient descent sabit sırayla ilerler.
/// </summary>
public sealed record ModelTrainingOptions
{
    /// <summary>Gradient descent adım boyu (standardize edilmiş feature'lar üzerinde).</summary>
    public double LearningRate { get; init; } = 0.1;

    /// <summary>Maksimum epoch sayısı (early stopping daha erken durdurabilir).</summary>
    public int Epochs { get; init; } = 400;

    /// <summary>L2 (ridge) regularizasyon katsayısı — aşırı öğrenmeyi sınırlar.</summary>
    public double L2Regularization { get; init; } = 1e-4;

    /// <summary>Feature standardizasyonu (train mean/std ile). LR yakınsaması için gereklidir.</summary>
    public bool Standardize { get; init; } = true;

    /// <summary>Kaç epoch'ta bir validation değerlendirilsin (early stopping için).</summary>
    public int EvalEveryEpochs { get; init; } = 10;

    /// <summary>Validation kaybı kaç değerlendirmede iyileşmezse eğitim durur (0 = kapalı).</summary>
    public int EarlyStoppingPatience { get; init; } = 6;

    public static ModelTrainingOptions Default => new();
}
