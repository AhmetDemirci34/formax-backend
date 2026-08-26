using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Infrastructure.Historical.Prediction.Confidence;

/// <summary>Validation verisinden confidence kalibrasyonu (artifact) üreten servis sözleşmesi.</summary>
public interface IConfidenceCalibrator
{
    /// <summary>
    /// Ensemble olasılıkları + alt-model olasılıkları + gerçek etiketlerden kalibrasyon fit eder. Deterministik.
    /// </summary>
    ConfidenceCalibration Fit(
        IReadOnlyList<double[]> ensembleProbabilities,
        IReadOnlyList<double[][]> perModelProbabilities,
        IReadOnlyList<int> labels);
}

/// <summary>
/// Confidence kalibratörü (v1). Deterministik. (1) Temperature'ı val log loss'unu minimize edecek şekilde
/// ızgara-araması ile seçer. (2) Her örnek için ham güvenilirlik (keskinlik×uyum) hesaplar. (3) Güvenilirliği
/// bin'lere ayırıp her bin'in GÖZLENEN doğruluğunu ölçer → confidence artık gerçek doğruluğu tahmin eder.
/// (4) Confidence Score yüzdeliklerinden Low/Medium/High eşiklerini belirler.
/// </summary>
public sealed class ConfidenceCalibrator : IConfidenceCalibrator
{
    private const int Bins = 10;
    private const double SharpnessWeight = 0.6;
    private const double AgreementWeight = 0.4;

    public ConfidenceCalibration Fit(
        IReadOnlyList<double[]> ensembleProbabilities,
        IReadOnlyList<double[][]> perModelProbabilities,
        IReadOnlyList<int> labels)
    {
        var n = ensembleProbabilities.Count;
        if (n == 0) throw new ArgumentException("Kalibrasyon için veri yok.", nameof(ensembleProbabilities));
        var classes = ensembleProbabilities[0].Length;

        // 1) TEMPERATURE — val log loss minimize (ızgara: T = k/20, k=10..80 → 0.50..4.00).
        var bestT = 1.0; var bestLoss = double.MaxValue;
        for (var k = 10; k <= 80; k++)
        {
            var t = k / 20.0;
            double loss = 0;
            for (var i = 0; i < n; i++)
            {
                var cp = ConfidenceEngine.TemperatureScale(ensembleProbabilities[i], t);
                loss += -Math.Log(Math.Clamp(cp[labels[i]], 1e-15, 1d));
            }
            loss /= n;
            if (loss < bestLoss - 1e-12) { bestLoss = loss; bestT = t; }
        }

        // 2) Ham güvenilirlik + doğruluk (seçilen T ile).
        var reliability = new double[n];
        var correct = new bool[n];
        for (var i = 0; i < n; i++)
        {
            var cp = ConfidenceEngine.TemperatureScale(ensembleProbabilities[i], bestT);
            var sharp = ConfidenceEngine.SharpnessNorm(cp, classes);
            var agree = ConfidenceEngine.Agreement(perModelProbabilities[i]);
            reliability[i] = Math.Clamp(SharpnessWeight * sharp + AgreementWeight * agree, 0d, 1d);
            correct[i] = ConfidenceEngine.ArgMax(cp) == labels[i];
        }

        // 3) Bin → gözlenen doğruluk.
        var edges = new double[Bins + 1];
        for (var b = 0; b <= Bins; b++) edges[b] = b / (double)Bins;
        var hit = new int[Bins];
        var cnt = new int[Bins];
        for (var i = 0; i < n; i++)
        {
            var b = BinIndex(reliability[i]);
            cnt[b]++;
            if (correct[i]) hit[b]++;
        }
        var binAcc = new double[Bins];
        for (var b = 0; b < Bins; b++) binAcc[b] = cnt[b] > 0 ? hit[b] / (double)cnt[b] : double.NaN;
        FillEmptyBins(binAcc);

        // 4) Confidence Score eşikleri (val yüzdelikleri → dengeli Low/Medium/High).
        var scores = new double[n];
        for (var i = 0; i < n; i++) scores[i] = binAcc[BinIndex(reliability[i])];
        Array.Sort(scores);
        var low = Percentile(scores, 0.33);
        var high = Percentile(scores, 0.66);
        if (high <= low) high = Math.Min(1d, low + 1e-6); // dejenere koruması

        return new ConfidenceCalibration
        {
            Classes = classes,
            Temperature = bestT,
            SharpnessWeight = SharpnessWeight,
            AgreementWeight = AgreementWeight,
            BinEdges = edges,
            BinAccuracy = binAcc,
            LowThreshold = low,
            HighThreshold = high
        };
    }

    private static int BinIndex(double reliability)
    {
        var b = (int)(reliability * Bins);
        if (b < 0) b = 0; if (b >= Bins) b = Bins - 1;
        return b;
    }

    /// <summary>Boş bin'leri en yakın dolu komşulardan lineer interpolasyonla doldurur (deterministik).</summary>
    private static void FillEmptyBins(double[] binAcc)
    {
        var anyFilled = binAcc.Any(v => !double.IsNaN(v));
        if (!anyFilled) { for (var b = 0; b < binAcc.Length; b++) binAcc[b] = 0.5; return; }

        for (var b = 0; b < binAcc.Length; b++)
        {
            if (!double.IsNaN(binAcc[b])) continue;
            var left = b - 1; while (left >= 0 && double.IsNaN(binAcc[left])) left--;
            var right = b + 1; while (right < binAcc.Length && double.IsNaN(binAcc[right])) right++;

            if (left < 0) binAcc[b] = binAcc[right];
            else if (right >= binAcc.Length) binAcc[b] = binAcc[left];
            else
            {
                var frac = (b - left) / (double)(right - left);
                binAcc[b] = binAcc[left] + frac * (binAcc[right] - binAcc[left]);
            }
        }
    }

    private static double Percentile(double[] sortedAsc, double q)
    {
        if (sortedAsc.Length == 0) return 0;
        var idx = (int)(q * (sortedAsc.Length - 1));
        if (idx < 0) idx = 0; if (idx >= sortedAsc.Length) idx = sortedAsc.Length - 1;
        return sortedAsc[idx];
    }
}
