using System.Globalization;
using Formax.DixonColes.Models;

namespace Formax.Calibration.Services;

public sealed class CalibrationDelta
{
    public required string Scope { get; init; }
    public required string Group { get; init; }
    public required int N { get; init; }

    /// <summary>Calibrated minus raw. Negative means calibration helped.</summary>
    public required double DeltaLogLoss { get; init; }
    public required double LogLossCiLow { get; init; }
    public required double LogLossCiHigh { get; init; }

    public required double DeltaBrier { get; init; }
    public required double BrierCiLow { get; init; }
    public required double BrierCiHigh { get; init; }

    public required double DeltaRps { get; init; }
    public required double RpsCiLow { get; init; }
    public required double RpsCiHigh { get; init; }

    public required int Resamples { get; init; }

    public const double NumericalNoiseFloor = 1e-12;

    public bool LogLossSignificant =>
        Math.Abs(DeltaLogLoss) > NumericalNoiseFloor &&
        ((LogLossCiLow < 0 && LogLossCiHigh < 0) || (LogLossCiLow > 0 && LogLossCiHigh > 0));

    public bool BrierSignificant =>
        Math.Abs(DeltaBrier) > NumericalNoiseFloor &&
        ((BrierCiLow < 0 && BrierCiHigh < 0) || (BrierCiLow > 0 && BrierCiHigh > 0));

    public bool RpsSignificant =>
        Math.Abs(DeltaRps) > NumericalNoiseFloor &&
        ((RpsCiLow < 0 && RpsCiHigh < 0) || (RpsCiLow > 0 && RpsCiHigh > 0));

    public string Verdict =>
        Math.Abs(DeltaLogLoss) <= NumericalNoiseFloor ? "IDENTICAL (below floating-point noise)"
        : !LogLossSignificant ? "NOT SIGNIFICANT"
        : DeltaLogLoss < 0 ? "CALIBRATION BETTER" : "RAW BETTER";

    private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);
    private static string Q(string s) => s.Contains(',') ? "\"" + s + "\"" : s;

    public static string CsvHeader =>
        "Scope,Group,N,DeltaLogLoss,LogLossCI95Low,LogLossCI95High,LogLossSignificant," +
        "DeltaBrier,BrierCI95Low,BrierCI95High,BrierSignificant," +
        "DeltaRPS,RpsCI95Low,RpsCI95High,RpsSignificant,Resamples,Verdict";

    public string ToCsv() => string.Join(',', Q(Scope), Q(Group), N.ToString(CultureInfo.InvariantCulture),
        F(DeltaLogLoss), F(LogLossCiLow), F(LogLossCiHigh), LogLossSignificant ? "True" : "False",
        F(DeltaBrier), F(BrierCiLow), F(BrierCiHigh), BrierSignificant ? "True" : "False",
        F(DeltaRps), F(RpsCiLow), F(RpsCiHigh), RpsSignificant ? "True" : "False",
        Resamples.ToString(CultureInfo.InvariantCulture), Q(Verdict));
}

/// <summary>
/// Paired bootstrap between the raw and the calibrated probabilities of the SAME matches.
///
/// Paired, because the two sets of probabilities describe identical matches: an unpaired interval
/// would be dominated by which matches happened to land in the resample rather than by what
/// calibration did to them. Deterministic: resample b uses seed baseSeed + b.
/// </summary>
public static class CalibrationBootstrap
{
    public const int DefaultResamples = 2000;
    public const int DefaultSeed = 20260821;

    private static (double ll, double brier, double rps) Losses(ProbTriple p, Outcome actual)
    {
        var ll = -Math.Log(Math.Max(p[actual], 1e-15));

        var yH = actual == Outcome.HomeWin ? 1.0 : 0.0;
        var yD = actual == Outcome.Draw ? 1.0 : 0.0;
        var yA = actual == Outcome.AwayWin ? 1.0 : 0.0;
        var brier = (p.Home - yH) * (p.Home - yH) + (p.Draw - yD) * (p.Draw - yD) + (p.Away - yA) * (p.Away - yA);

        var c1 = p.Home - yH;
        var c2 = p.Home + p.Draw - (yH + yD);
        var rps = (c1 * c1 + c2 * c2) / 2.0;

        return (ll, brier, rps);
    }

    public static CalibrationDelta Compare(
        IReadOnlyList<ProbTriple> raw, IReadOnlyList<ProbTriple> calibrated, IReadOnlyList<Outcome> actual,
        string scope, string group, int resamples = DefaultResamples, int seed = DefaultSeed)
    {
        var n = raw.Count;
        var dLl = new double[n];
        var dBr = new double[n];
        var dRp = new double[n];
        for (var i = 0; i < n; i++)
        {
            var a = Losses(calibrated[i], actual[i]);
            var b = Losses(raw[i], actual[i]);
            dLl[i] = a.ll - b.ll;
            dBr[i] = a.brier - b.brier;
            dRp[i] = a.rps - b.rps;
        }

        var obsLl = n == 0 ? double.NaN : dLl.Sum() / n;
        var obsBr = n == 0 ? double.NaN : dBr.Sum() / n;
        var obsRp = n == 0 ? double.NaN : dRp.Sum() / n;

        var bLl = new double[resamples];
        var bBr = new double[resamples];
        var bRp = new double[resamples];
        Parallel.For(0, resamples, r =>
        {
            var rng = new Random(seed + r);
            double sl = 0, sb = 0, sr = 0;
            for (var i = 0; i < n; i++)
            {
                var k = rng.Next(n);
                sl += dLl[k]; sb += dBr[k]; sr += dRp[k];
            }
            bLl[r] = sl / n; bBr[r] = sb / n; bRp[r] = sr / n;
        });
        Array.Sort(bLl); Array.Sort(bBr); Array.Sort(bRp);

        return new CalibrationDelta
        {
            Scope = scope,
            Group = group,
            N = n,
            DeltaLogLoss = obsLl,
            LogLossCiLow = Percentile(bLl, 0.025),
            LogLossCiHigh = Percentile(bLl, 0.975),
            DeltaBrier = obsBr,
            BrierCiLow = Percentile(bBr, 0.025),
            BrierCiHigh = Percentile(bBr, 0.975),
            DeltaRps = obsRp,
            RpsCiLow = Percentile(bRp, 0.025),
            RpsCiHigh = Percentile(bRp, 0.975),
            Resamples = resamples
        };
    }

    private static double Percentile(double[] sorted, double q)
    {
        if (sorted.Length == 0) return double.NaN;
        var pos = q * (sorted.Length - 1);
        var lo = (int)Math.Floor(pos);
        var hi = (int)Math.Ceiling(pos);
        return lo == hi ? sorted[lo] : sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }
}
