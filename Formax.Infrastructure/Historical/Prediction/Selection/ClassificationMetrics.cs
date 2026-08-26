using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.Historical.Prediction.Selection;

/// <summary>
/// Bir modelin bir veri seti üzerindeki sınıflandırma metrikleri. Tüm adaylar için AYNI hesaplayıcı
/// (<see cref="ClassificationMetricsCalculator"/>) ile AYNI veri üzerinde üretilir → adil karşılaştırma.
/// Precision/Recall/F1 makro-ortalamadır (sınıflar eşit ağırlıklı; dengesiz 1X2 için doğru seçim).
/// </summary>
public sealed record ClassificationMetrics
{
    public int Samples { get; init; }
    public double Accuracy { get; init; }
    public double MacroPrecision { get; init; }
    public double MacroRecall { get; init; }
    public double MacroF1 { get; init; }
    /// <summary>Çok sınıflı log loss (cross-entropy) — düşük iyi. Olasılık kalitesi (Probability Engine için birincil).</summary>
    public double LogLoss { get; init; }
    /// <summary>Multiclass Brier — düşük iyi. Olasılık kalibrasyonu.</summary>
    public double Brier { get; init; }
    public double[] PerClassPrecision { get; init; } = Array.Empty<double>();
    public double[] PerClassRecall { get; init; } = Array.Empty<double>();
    public double[] PerClassF1 { get; init; } = Array.Empty<double>();
}

/// <summary>Olasılık tahminleri + gerçek etiketlerden sınıflandırma metriklerini hesaplar (saf, deterministik).</summary>
public static class ClassificationMetricsCalculator
{
    public static ClassificationMetrics Evaluate(IReadOnlyList<double[]> probabilities, IReadOnlyList<int> actual, int classes = 3)
    {
        var n = probabilities.Count;
        var tp = new int[classes];
        var fp = new int[classes];
        var fn = new int[classes];
        int correct = 0;
        double logloss = 0, brier = 0;

        for (var i = 0; i < n; i++)
        {
            var p = probabilities[i];
            var y = actual[i];

            var pred = 0;
            for (var c = 1; c < classes; c++) if (p[c] > p[pred]) pred = c;

            if (pred == y) { correct++; tp[y]++; }
            else { fp[pred]++; fn[y]++; }

            logloss += -Math.Log(Math.Clamp(p[y], 1e-15, 1d));
            for (var c = 0; c < classes; c++) { var t = y == c ? 1d : 0d; brier += (p[c] - t) * (p[c] - t); }
        }

        var prec = new double[classes];
        var rec = new double[classes];
        var f1 = new double[classes];
        double mP = 0, mR = 0, mF = 0;
        for (var c = 0; c < classes; c++)
        {
            prec[c] = tp[c] + fp[c] == 0 ? 0 : tp[c] / (double)(tp[c] + fp[c]);
            rec[c] = tp[c] + fn[c] == 0 ? 0 : tp[c] / (double)(tp[c] + fn[c]);
            f1[c] = prec[c] + rec[c] == 0 ? 0 : 2 * prec[c] * rec[c] / (prec[c] + rec[c]);
            mP += prec[c]; mR += rec[c]; mF += f1[c];
        }

        return new ClassificationMetrics
        {
            Samples = n,
            Accuracy = n == 0 ? 0 : correct / (double)n,
            MacroPrecision = mP / classes,
            MacroRecall = mR / classes,
            MacroF1 = mF / classes,
            LogLoss = n == 0 ? 0 : logloss / n,
            Brier = n == 0 ? 0 : brier / n,
            PerClassPrecision = prec,
            PerClassRecall = rec,
            PerClassF1 = f1
        };
    }
}
