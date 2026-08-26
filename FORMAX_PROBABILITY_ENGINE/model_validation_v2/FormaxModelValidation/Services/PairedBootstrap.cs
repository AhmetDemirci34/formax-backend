using System.Globalization;
using Formax.ModelValidation.Models;

namespace Formax.ModelValidation.Services;

public sealed class PairComparison
{
    public required string Scope { get; init; }
    public required string Group { get; init; }
    public required string ModelA { get; init; }
    public required string ModelB { get; init; }
    public required int N { get; init; }
    /// <summary>Mean log loss of A minus mean log loss of B. Negative means A is better.</summary>
    public required double DeltaLogLoss { get; init; }
    public required double CiLow { get; init; }
    public required double CiHigh { get; init; }
    /// <summary>Share of bootstrap resamples in which A had the lower log loss.</summary>
    public required double ShareAWins { get; init; }
    public required int Resamples { get; init; }

    /// <summary>
    /// Below this, a "difference" is floating-point noise, not a difference. Two models that are
    /// mathematically the same - the bivariate Poisson with a zero shared component IS independent
    /// Poisson - disagree in the last bits of the mantissa, and a paired bootstrap will happily
    /// report that tiny disagreement as a consistent win. It is not one.
    /// </summary>
    public const double NumericalNoiseFloor = 1e-12;

    /// <summary>Significant only when the whole interval sits on one side of zero AND the gap is larger than numerical noise.</summary>
    public bool CiExcludesZero =>
        Math.Abs(DeltaLogLoss) > NumericalNoiseFloor &&
        ((CiLow < 0 && CiHigh < 0) || (CiLow > 0 && CiHigh > 0));

    private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);

    public static string CsvHeader =>
        "Scope,Group,ModelA,ModelB,N,DeltaLogLoss,CI95Low,CI95High,ShareOfResamplesAWins,Resamples,CiExcludesZero,Verdict";

    public string Verdict =>
        Math.Abs(DeltaLogLoss) <= NumericalNoiseFloor ? "IDENTICAL (difference is below floating-point noise)"
        : !CiExcludesZero ? "NOT SIGNIFICANT"
        : DeltaLogLoss < 0 ? "A BETTER" : "B BETTER";

    public string ToCsv() => string.Join(',',
        Scope, Group, ModelA, ModelB, N.ToString(CultureInfo.InvariantCulture),
        F(DeltaLogLoss), F(CiLow), F(CiHigh), F(ShareAWins),
        Resamples.ToString(CultureInfo.InvariantCulture),
        CiExcludesZero ? "True" : "False", Verdict);
}

/// <summary>
/// Paired bootstrap over matches.
///
/// The models are compared match by match on the SAME resample - if a resample happens to contain
/// an unusual run of draws, every model sees that same run. That is the only honest way to ask
/// whether a 0.001 log loss gap is a real difference between models or just an artefact of which
/// matches happened to be in the test window.
///
/// Deterministic: the seed of resample b is baseSeed + b, so the interval is reproducible.
/// </summary>
public static class PairedBootstrap
{
    public const int DefaultResamples = 2000;
    public const int DefaultSeed = 20260821;

    public static PairComparison Compare(
        IReadOnlyList<MatchPrediction> predictions, ModelId a, ModelId b,
        string scope, string group, int resamples = DefaultResamples, int seed = DefaultSeed)
    {
        var n = predictions.Count;
        var la = new double[n];
        var lb = new double[n];
        for (var i = 0; i < n; i++)
        {
            var p = predictions[i];
            la[i] = -Math.Log(Math.Max(p.Probabilities[(int)a][p.Actual], 1e-15));
            lb[i] = -Math.Log(Math.Max(p.Probabilities[(int)b][p.Actual], 1e-15));
        }

        var observed = n == 0 ? double.NaN : (la.Sum() - lb.Sum()) / n;

        var deltas = new double[resamples];
        Parallel.For(0, resamples, r =>
        {
            var rng = new Random(seed + r);
            double sum = 0;
            for (var i = 0; i < n; i++)
            {
                var k = rng.Next(n);
                sum += la[k] - lb[k];
            }
            deltas[r] = sum / n;
        });

        Array.Sort(deltas);
        var low = Percentile(deltas, 0.025);
        var high = Percentile(deltas, 0.975);
        var wins = deltas.Count(d => d < 0) / (double)resamples;

        return new PairComparison
        {
            Scope = scope,
            Group = group,
            ModelA = ModelIds.Name(a),
            ModelB = ModelIds.Name(b),
            N = n,
            DeltaLogLoss = observed,
            CiLow = low,
            CiHigh = high,
            ShareAWins = wins,
            Resamples = resamples
        };
    }

    private static double Percentile(double[] sorted, double q)
    {
        if (sorted.Length == 0) return double.NaN;
        var pos = q * (sorted.Length - 1);
        var lo = (int)Math.Floor(pos);
        var hi = (int)Math.Ceiling(pos);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }
}
