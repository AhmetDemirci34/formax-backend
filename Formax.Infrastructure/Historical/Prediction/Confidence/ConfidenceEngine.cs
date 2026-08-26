using System;
using System.Collections.Generic;
using System.Globalization;

namespace Formax.Infrastructure.Historical.Prediction.Confidence;

/// <summary>Bir tahminin confidence'ını değerlendiren servis (olasılıktan ayrı).</summary>
public interface IConfidenceEngine
{
    /// <summary>
    /// Ensemble olasılığı + her alt-modelin olasılığından confidence üretir. Agreement (alt-model uyumu)
    /// yalnız birleşik olasılıktan çıkarılamadığı için AYNI ensembleProbability farklı perModel dağılımıyla
    /// FARKLI confidence alır.
    /// </summary>
    ConfidenceAssessment Assess(double[] ensembleProbability, IReadOnlyList<double[]> perModelProbabilities);
}

/// <summary>
/// Confidence Engine (v1). Deterministik + saf. Verilmiş <see cref="ConfidenceCalibration"/> ile: (1) olasılığı
/// temperature scaling ile kalibre eder, (2) keskinlik × uyumdan ham güvenilirlik üretir, (3) bunu validation'da
/// öğrenilmiş bin→doğruluk eşlemesiyle kalibre confidence'a çevirir, (4) seviye (Low/Medium/High) ve gerekçe üretir.
/// Probability ve Confidence AYRI üretilir.
/// </summary>
public sealed class ConfidenceEngine : IConfidenceEngine
{
    private readonly ConfidenceCalibration _cal;

    public ConfidenceEngine(ConfidenceCalibration calibration) => _cal = calibration;

    public ConfidenceAssessment Assess(double[] ensembleProbability, IReadOnlyList<double[]> perModelProbabilities)
    {
        var calibrated = TemperatureScale(ensembleProbability, _cal.Temperature);
        var predicted = ArgMax(calibrated);
        var sharpness = SharpnessNorm(calibrated, _cal.Classes);
        var agreement = Agreement(perModelProbabilities);

        var reliability = Math.Clamp(_cal.SharpnessWeight * sharpness + _cal.AgreementWeight * agreement, 0d, 1d);
        var score = MapReliabilityToAccuracy(reliability, _cal);
        var level = score < _cal.LowThreshold ? ConfidenceLevel.Low
                  : score < _cal.HighThreshold ? ConfidenceLevel.Medium
                  : ConfidenceLevel.High;

        return new ConfidenceAssessment
        {
            CalibratedProbability = calibrated,
            PredictedClass = predicted,
            PredictionReliability = reliability,
            ConfidenceScore = score,
            Level = level,
            Reason = BuildReason(level, sharpness, agreement)
        };
    }

    // ── Paylaşılan deterministik matematik (Calibrator da kullanır) ──

    /// <summary>Temperature scaling: softmax(log(p)/T). T&gt;1 yumuşatır, T&lt;1 keskinleştirir.</summary>
    public static double[] TemperatureScale(double[] p, double temperature)
    {
        var t = temperature <= 1e-6 ? 1e-6 : temperature;
        var logits = new double[p.Length];
        for (var c = 0; c < p.Length; c++) logits[c] = Math.Log(Math.Clamp(p[c], 1e-12, 1d)) / t;
        return ProbabilityModel.Softmax(logits);
    }

    /// <summary>Normalize keskinlik: (max - 1/K)/(1 - 1/K) ∈ [0,1]. 1 = tek sınıfa yakın, 0 = düzgün dağılım.</summary>
    public static double SharpnessNorm(double[] p, int classes)
    {
        double max = 0; for (var c = 0; c < p.Length; c++) if (p[c] > max) max = p[c];
        var floor = 1d / classes;
        return Math.Clamp((max - floor) / (1d - floor), 0d, 1d);
    }

    /// <summary>Alt-model uyumu = 1 - ortalama ikili toplam-varyasyon mesafesi ∈ [0,1]. Tek model → 1.</summary>
    public static double Agreement(IReadOnlyList<double[]> perModel)
    {
        var n = perModel.Count;
        if (n < 2) return 1d;
        double sumTv = 0; var pairs = 0;
        for (var a = 0; a < n; a++)
            for (var b = a + 1; b < n; b++)
            {
                double tv = 0;
                var pa = perModel[a]; var pb = perModel[b];
                for (var c = 0; c < pa.Length; c++) tv += Math.Abs(pa[c] - pb[c]);
                sumTv += 0.5 * tv; pairs++;
            }
        return Math.Clamp(1d - sumTv / pairs, 0d, 1d);
    }

    /// <summary>Ham güvenilirliği, validation bin doğruluğuna eşler (kalibre confidence).</summary>
    public static double MapReliabilityToAccuracy(double reliability, ConfidenceCalibration cal)
    {
        if (cal.BinEdges.Length < 2 || cal.BinAccuracy.Length == 0) return reliability;
        var bins = cal.BinAccuracy.Length;
        var b = (int)(reliability * bins);
        if (b < 0) b = 0; if (b >= bins) b = bins - 1;
        return cal.BinAccuracy[b];
    }

    public static int ArgMax(double[] p)
    {
        var best = 0; for (var c = 1; c < p.Length; c++) if (p[c] > p[best]) best = c;
        return best;
    }

    private static string BuildReason(ConfidenceLevel level, double sharpness, double agreement)
    {
        var head = level switch { ConfidenceLevel.High => "Yüksek güven", ConfidenceLevel.Medium => "Orta güven", _ => "Düşük güven" };
        var agreeTxt = agreement >= 0.8 ? "modeller hemfikir" : agreement >= 0.55 ? "modeller kısmen uyumlu" : "modeller anlaşamıyor";
        var sharpTxt = sharpness >= 0.5 ? "favori belirgin" : sharpness >= 0.25 ? "favori orta" : "olasılıklar birbirine yakın";
        var s = sharpness.ToString("P0", CultureInfo.InvariantCulture);
        var a = agreement.ToString("P0", CultureInfo.InvariantCulture);
        return $"{head} — {agreeTxt} (uyum={a}), {sharpTxt} (keskinlik={s}).";
    }
}
