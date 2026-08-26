using System;

namespace Formax.Infrastructure.Historical.Prediction.Confidence;

/// <summary>Güven seviyesi — olasılıktan AYRI bir kavram (tahminin ne kadar güvenilir olduğu).</summary>
public enum ConfidenceLevel
{
    Low,
    Medium,
    High
}

/// <summary>
/// Confidence kalibrasyon artifact'ı (serileştirilebilir). Validation'da fit edilir; Confidence Engine bunu
/// kullanarak ham güvenilirlik sinyalini GERÇEK doğruluğa kalibre eder. Model artifact'ine confidence desteği
/// bu blokla eklenir (ensemble ile birlikte kaydedilip yüklenir).
/// </summary>
public sealed record ConfidenceCalibration
{
    public string Version { get; init; } = "v1";
    public int Classes { get; init; } = 3;

    /// <summary>Olasılık kalibrasyonu (temperature scaling): softmax(log(p)/T). Val log loss'unu minimize eden T.</summary>
    public double Temperature { get; init; } = 1.0;

    /// <summary>Ham güvenilirlik ağırlıkları: reliability = wSharp*keskinlik + wAgree*uyum.</summary>
    public double SharpnessWeight { get; init; } = 0.6;
    public double AgreementWeight { get; init; } = 0.4;

    /// <summary>Güvenilirlik→doğruluk kalibrasyonu için bin sınırları (uzunluk = bin sayısı + 1).</summary>
    public double[] BinEdges { get; init; } = Array.Empty<double>();
    /// <summary>Her bin'in validation'daki gözlenen doğruluğu (= o bölgedeki P(doğru) tahmini).</summary>
    public double[] BinAccuracy { get; init; } = Array.Empty<double>();

    /// <summary>Confidence Score eşikleri (validation yüzdelikleri): &lt;Low → Low, &lt;High → Medium, ≥High → High.</summary>
    public double LowThreshold { get; init; } = 0.40;
    public double HighThreshold { get; init; } = 0.55;
}

/// <summary>
/// Tek bir tahmin için confidence değerlendirmesi. PROBABILITY ile CONFIDENCE tamamen ayrı: CalibratedProbability
/// olasılık dağılımıdır; ConfidenceScore/Level/Reliability tahminin güvenilirliğini ölçer.
/// </summary>
public sealed record ConfidenceAssessment
{
    /// <summary>Kalibre edilmiş olasılık dağılımı [H, D, A] (toplam 1). (Olasılık — confidence'tan ayrı.)</summary>
    public required double[] CalibratedProbability { get; init; }
    /// <summary>En olası sınıf indeksi (0=H, 1=D, 2=A).</summary>
    public int PredictedClass { get; init; }

    /// <summary>Ham güvenilirlik sinyali [0,1] = keskinlik × uyum (kalibrasyon öncesi).</summary>
    public double PredictionReliability { get; init; }
    /// <summary>Kalibre confidence [0,1] = tahminin doğru olma olasılığı tahmini (gerçek doğrulukla ilişkili).</summary>
    public double ConfidenceScore { get; init; }
    public ConfidenceLevel Level { get; init; }
    /// <summary>İnsan-okunur gerekçe (hangi faktörler güveni belirledi).</summary>
    public required string Reason { get; init; }
}
