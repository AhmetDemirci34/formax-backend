using System.Globalization;
using Formax.Calibration.Models;
using Formax.DixonColes.Models;

namespace Formax.Calibration.Services;

/// <summary>One row of a reliability curve: what we said, what happened, and on how many matches.</summary>
public sealed class ReliabilityRow
{
    public required string Method { get; init; }
    public required string Segment { get; init; }
    public required string View { get; init; }
    public required string Band { get; init; }
    public required double Lower { get; init; }
    public required double Upper { get; init; }
    public required int N { get; init; }
    public required double PredictedMean { get; init; }
    public required double ActualFrequency { get; init; }
    public double Difference => PredictedMean - ActualFrequency;

    private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);

    public static string CsvHeader =>
        "Method,Segment,View,Band,BandLower,BandUpper,SampleSize,PredictedMean,ActualFrequency,Difference";

    public string ToCsv() => string.Join(',', Method, Segment, View, Band,
        F(Lower), F(Upper), N.ToString(CultureInfo.InvariantCulture),
        F(PredictedMean), F(ActualFrequency), F(Difference));
}

/// <summary>
/// Calibration error measures.
///
/// CALIBRATION ERROR here means class-wise expected calibration error: every match contributes
/// three (probability, happened?) pairs - one per outcome - they are pooled into equal-width bins
/// and the error is the sample-weighted mean gap between what was predicted in a bin and what
/// actually happened in it.
///
///   ECE = sum_b (n_b / N) * | mean(p in b) - frequency(y in b) |
///
/// Pooled over all three classes rather than only the argmax, because FORMAX shows all three
/// percentages to a user; a model that is honest about its favourite and dishonest about draws is
/// not calibrated. MCE is the worst single bin, reported so one badly broken region cannot hide
/// behind a good average.
/// </summary>
public static class CalibrationMetrics
{
    public const int DefaultBins = 10;

    public static (double ece, double mce) Error(IEnumerable<(double p, double y)> pairs, int bins = DefaultBins)
    {
        var sumP = new double[bins];
        var sumY = new double[bins];
        var n = new int[bins];
        var total = 0;

        foreach (var (p, y) in pairs)
        {
            var b = (int)(p * bins);
            if (b >= bins) b = bins - 1;
            if (b < 0) b = 0;
            sumP[b] += p; sumY[b] += y; n[b]++; total++;
        }

        if (total == 0) return (double.NaN, double.NaN);

        var ece = 0.0; var mce = 0.0;
        for (var b = 0; b < bins; b++)
        {
            if (n[b] == 0) continue;
            var gap = Math.Abs(sumP[b] / n[b] - sumY[b] / n[b]);
            ece += (double)n[b] / total * gap;
            if (gap > mce) mce = gap;
        }
        return (ece, mce);
    }

    /// <summary>All three class probabilities of every match, as (predicted, happened) pairs.</summary>
    public static IEnumerable<(double p, double y)> AllClassPairs(IEnumerable<(ProbTriple p, Outcome actual)> data)
    {
        foreach (var (p, actual) in data)
            for (var k = 0; k < 3; k++)
                yield return (p[(Outcome)k], (int)actual == k ? 1.0 : 0.0);
    }

    /// <summary>Only the highest probability of each match - what a user reads as "the pick".</summary>
    public static IEnumerable<(double p, double y)> TopClassPairs(IEnumerable<(ProbTriple p, Outcome actual)> data)
    {
        foreach (var (p, actual) in data)
            yield return (p.Max, p.ArgMax == actual ? 1.0 : 0.0);
    }

    /// <summary>The decile bands. The task asks for 50-60 upward by name; the lower bands are kept so the curve is complete.</summary>
    public static readonly double[] BandEdges = { 0.0, 0.10, 0.20, 0.30, 0.40, 0.50, 0.60, 0.70, 0.80, 0.90, 1.0 };

    public static List<ReliabilityRow> Bands(
        IEnumerable<(double p, double y)> pairs, string method, string segment, string view)
    {
        var k = BandEdges.Length - 1;
        var sumP = new double[k];
        var sumY = new double[k];
        var n = new int[k];

        foreach (var (p, y) in pairs)
        {
            var b = -1;
            for (var i = 0; i < k; i++)
                if (p >= BandEdges[i] && (p < BandEdges[i + 1] || (i == k - 1 && p <= 1.0))) { b = i; break; }
            if (b < 0) continue;
            sumP[b] += p; sumY[b] += y; n[b]++;
        }

        var rows = new List<ReliabilityRow>();
        for (var i = 0; i < k; i++)
            rows.Add(new ReliabilityRow
            {
                Method = method,
                Segment = segment,
                View = view,
                Band = $"{BandEdges[i] * 100:0}-{BandEdges[i + 1] * 100:0}%",
                Lower = BandEdges[i],
                Upper = BandEdges[i + 1],
                N = n[i],
                PredictedMean = n[i] == 0 ? double.NaN : sumP[i] / n[i],
                ActualFrequency = n[i] == 0 ? double.NaN : sumY[i] / n[i]
            });
        return rows;
    }
}
