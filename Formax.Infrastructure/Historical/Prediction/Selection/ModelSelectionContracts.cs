using System.Collections.Generic;
using Formax.Infrastructure.Historical.Dataset;
using DatasetV1 = Formax.Infrastructure.Historical.Dataset.Dataset;

namespace Formax.Infrastructure.Historical.Prediction.Selection;

/// <summary>
/// Eğitilmiş, tahmin üretebilen bir model (heterojen — LR, LightGBM, ... aynı arayüz). Bir feature
/// vektöründen sınıf olasılıkları [H, D, A] (toplam 1) üretir. Deterministik olmalıdır.
/// </summary>
public interface ITrainedModel
{
    string Name { get; }
    double[] PredictProba(double[] features);
    /// <summary>Serileştirilmiş/kalıcı model boyutu (byte) — karşılaştırma metriği.</summary>
    long ModelSizeBytes { get; }
    /// <summary>Modeli kendi-yeterli artifact byte'larına serileştirir (LR=JSON, LightGBM=ML.NET zip). Ensemble kaydı bunu kullanır.</summary>
    byte[] Serialize();
}

/// <summary>
/// Bir model ADAYI: verilmiş Dataset v1 üzerinde eğitilip <see cref="ITrainedModel"/> üretir. Yeni bir model
/// eklemek = bu arayüzü uygulayıp DI'a IModelCandidate olarak kaydetmek (selector otomatik dahil eder).
/// Aday, dataset'i KENDİ OKUMAZ — çerçeve tek Dataset v1 örneğini tüm adaylara verir (adil + tek kaynak).
/// </summary>
public interface IModelCandidate
{
    string Name { get; }
    ITrainedModel Train(DatasetV1 dataset);
    /// <summary>
    /// <see cref="ITrainedModel.Serialize"/> byte'larından modeli geri yükler (aynı aday tipi = hem eğitici hem
    /// yükleyici). Ensemble deposu, alt-modelleri isimlerine göre bununla yeniden kurar. Yeni model eklemek =
    /// bu aday tipini de sağlamak → XGBoost/CatBoost aynı yapıya doğal girer.
    /// </summary>
    ITrainedModel Load(byte[] artifact);
}

/// <summary>Tek bir modelin karşılaştırma sonucu (tüm metrikler + kaynak ölçümleri).</summary>
public sealed record ModelEvaluation
{
    public required string ModelName { get; init; }
    public required ClassificationMetrics Metrics { get; init; }
    public long TrainingTimeMs { get; init; }
    /// <summary>1000 tahmin başına milisaniye (inference hızı).</summary>
    public double PredictionMsPer1k { get; init; }
    public long ModelSizeBytes { get; init; }
    /// <summary>Objektif sıralama skoru (düşük = daha iyi; birincil = validation log loss).</summary>
    public double SelectionScore { get; init; }
}

/// <summary>Model Selection nihai raporu: tüm değerlendirmeler + objektif kazanan + gerekçe.</summary>
public sealed record ModelSelectionReport
{
    public required IReadOnlyList<ModelEvaluation> Evaluations { get; init; }
    public required string RecommendedModel { get; init; }
    public required string Rationale { get; init; }
    public required DatasetMetadata DatasetMetadata { get; init; }
    /// <summary>Karşılaştırmaya DAHİL EDİLMEYEN modeller ve nedeni (şeffaflık).</summary>
    public IReadOnlyList<string> ExcludedModels { get; init; } = System.Array.Empty<string>();
}

/// <summary>Birden çok modeli aynı Dataset v1 üzerinde eğitip karşılaştıran ve objektif kazananı seçen servis.</summary>
public interface IModelSelector
{
    System.Threading.Tasks.Task<ModelSelectionReport> SelectAsync(System.Threading.CancellationToken cancellationToken = default);
}
