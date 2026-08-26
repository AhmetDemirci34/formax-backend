using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.Historical.Prediction;

/// <summary>Bir model değerlendirmesinin metrikleri (train veya validation üzerinde).</summary>
public sealed record ModelMetrics
{
    public int Samples { get; init; }
    /// <summary>Doğru sınıflandırma oranı (argmax).</summary>
    public double Accuracy { get; init; }
    /// <summary>Çok sınıflı log loss (cross-entropy) — düşük iyi. Olasılık kalitesinin ölçüsü.</summary>
    public double LogLoss { get; init; }
    /// <summary>Multiclass Brier skoru — düşük iyi. Olasılık kalibrasyonu ölçüsü.</summary>
    public double Brier { get; init; }
    /// <summary>Sınıf-önceliğine (train dağılımı) göre tahmin eden naive baseline'ın log loss'u — model bunu YENMELİ.</summary>
    public double BaselineLogLoss { get; init; }
    /// <summary>Çoğunluk sınıfını her zaman tahmin eden baseline'ın doğruluğu — model bunu YENMELİ.</summary>
    public double MajorityBaselineAccuracy { get; init; }
    /// <summary>Sınıf başına recall (0=H, 1=D, 2=A).</summary>
    public double[] PerClassRecall { get; init; } = Array.Empty<double>();
}

/// <summary>
/// Probability Engine'in eğitilmiş modeli (v1: multinomial logistic regression / softmax). Serileştirilebilir
/// artifact: standardizasyon (mean/std) + ağırlıklar + bias + hyperparametreler + metrikler. Bir feature
/// vektöründen 1X2 olasılıkları (H/D/A) üretir — çıktı deterministik. Yalnız Feature Store / Dataset v1'den
/// türetilmiştir; tahmin sırasında hiçbir dış veri okumaz (saf fonksiyon).
/// </summary>
public sealed record ProbabilityModel
{
    public string Version { get; init; } = "v1";
    public string Algorithm { get; init; } = "multinomial-logistic-regression";
    public DateTime TrainedAtUtc { get; init; }

    public int FeatureCount { get; init; }
    public IReadOnlyList<string> FeatureNames { get; init; } = Array.Empty<string>();
    /// <summary>Sınıf etiketleri, label indeksi sırasında: [0]=H, [1]=D, [2]=A.</summary>
    public IReadOnlyList<string> Classes { get; init; } = new[] { "H", "D", "A" };

    /// <summary>Feature standardizasyonu: ortalama (train'den). Uzunluk = FeatureCount.</summary>
    public double[] Mean { get; init; } = Array.Empty<double>();
    /// <summary>Feature standardizasyonu: std (train'den; 0 ise 1'e sabitlenir). Uzunluk = FeatureCount.</summary>
    public double[] Std { get; init; } = Array.Empty<double>();

    /// <summary>Ağırlıklar [sınıf][feature].</summary>
    public double[][] Weights { get; init; } = Array.Empty<double[]>();
    /// <summary>Bias [sınıf].</summary>
    public double[] Bias { get; init; } = Array.Empty<double>();

    public ModelTrainingOptions Hyperparameters { get; init; } = new();
    public ModelMetrics? TrainMetrics { get; init; }
    public ModelMetrics? ValidationMetrics { get; init; }

    /// <summary>Feature vektöründen sınıf olasılıkları [H, D, A] (toplam = 1). Deterministik, saf.</summary>
    public double[] PredictProba(double[] features)
    {
        var classes = Bias.Length;
        var logits = new double[classes];
        for (var c = 0; c < classes; c++)
        {
            var w = Weights[c];
            double z = Bias[c];
            for (var j = 0; j < features.Length; j++)
            {
                var std = Std.Length > j ? Std[j] : 1d;
                var mean = Mean.Length > j ? Mean[j] : 0d;
                var x = std > 0 ? (features[j] - mean) / std : 0d;
                z += w[j] * x;
            }
            logits[c] = z;
        }
        return Softmax(logits);
    }

    /// <summary>En yüksek olasılıklı sınıf indeksi (0=H, 1=D, 2=A).</summary>
    public int PredictLabel(double[] features)
    {
        var p = PredictProba(features);
        var best = 0;
        for (var c = 1; c < p.Length; c++) if (p[c] > p[best]) best = c;
        return best;
    }

    /// <summary>Sayısal-kararlı softmax (en büyük logit çıkarılır).</summary>
    public static double[] Softmax(double[] logits)
    {
        var max = double.NegativeInfinity;
        foreach (var z in logits) if (z > max) max = z;
        double sum = 0;
        var p = new double[logits.Length];
        for (var c = 0; c < logits.Length; c++) { p[c] = Math.Exp(logits[c] - max); sum += p[c]; }
        if (sum <= 0) { for (var c = 0; c < p.Length; c++) p[c] = 1d / p.Length; return p; }
        for (var c = 0; c < p.Length; c++) p[c] /= sum;
        return p;
    }
}
