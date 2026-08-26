using System.Globalization;
using Formax.Calibration.Models;
using Formax.Calibration.Services;
using Formax.DixonColes.Models;

namespace Formax.Calibration.Calibrators;

/// <summary>
/// One-vs-rest isotonic regression, made simplex preserving by renormalisation.
///
/// HOW IT IS APPLIED (the task asks for this to be stated explicitly):
///
///   1. Three independent binary problems are formed: "home won?", "draw?", "away won?".
///   2. For each, a non-decreasing map g_k from the raw probability p_k to the observed frequency
///      is fitted by Pool Adjacent Violators. PAVA is parameter free - no bin count, no smoothing
///      constant, nothing that could be tuned on a test set.
///   3. For a new match the three maps are applied independently, which does NOT give a
///      distribution: g_H(p_H) + g_D(p_D) + g_A(p_A) is generally not 1.
///   4. The three values are floored and divided by their sum. This is the standard
///      simplex-preserving fix and the only step of the four that is not itself isotonic.
///
/// Step 4 is a real compromise and is reported as one: renormalisation can move a class away from
/// the frequency isotonic regression just assigned it, so per-class calibration is not preserved
/// exactly. The alternative - fitting on the simplex directly - is a different and much larger
/// method, and this phase is not the place to invent one.
///
/// Between fitted blocks the map is a step function: a query lands in the block whose raw-value
/// range contains it, or, if it falls between two blocks, is linearly interpolated between their
/// values. Below the first block it takes the first value, above the last it takes the last.
/// </summary>
public sealed class IsotonicOvr : ICalibrator
{
    private readonly double[][] _xLow = new double[3][];
    private readonly double[][] _xHigh = new double[3][];
    private readonly double[][] _value = new double[3][];

    public string Name => "ISOTONIC_OVR";
    /// <summary>Non-parametric: the "parameter count" is the number of isotonic blocks the data produced.</summary>
    public int ParameterCount => _value[0] is null ? 0 : _value.Sum(v => v?.Length ?? 0);
    public bool IsFitted { get; private set; }

    public void Fit(IReadOnlyList<CalibrationSample> history)
    {
        if (history.Count == 0) { IsFitted = false; return; }

        for (var k = 0; k < 3; k++)
        {
            var x = new double[history.Count];
            var y = new double[history.Count];
            for (var i = 0; i < history.Count; i++)
            {
                x[i] = history[i].Raw[(Outcome)k];
                y[i] = history[i].Y(k);
            }
            var (lo, hi, v) = Numerics.Pava(x, y);
            _xLow[k] = lo; _xHigh[k] = hi; _value[k] = v;
        }
        IsFitted = true;
    }

    private double Map(int k, double p)
    {
        var lo = _xLow[k]; var hi = _xHigh[k]; var v = _value[k];
        if (v.Length == 0) return p;
        if (p <= lo[0]) return v[0];
        if (p >= hi[^1]) return v[^1];

        // binary search for the block whose range contains p, or the gap just after a block
        var a = 0; var b = v.Length - 1;
        while (a < b)
        {
            var mid = (a + b) / 2;
            if (p > hi[mid]) a = mid + 1; else b = mid;
        }
        if (p >= lo[a] && p <= hi[a]) return v[a];

        // p sits in the gap between block a-1 and block a: interpolate so the map stays monotone
        var left = a - 1;
        if (left < 0) return v[a];
        var span = lo[a] - hi[left];
        if (span <= 0) return v[a];
        var t = (p - hi[left]) / span;
        return v[left] + (v[a] - v[left]) * t;
    }

    public ProbTriple Apply(ProbTriple raw)
    {
        if (!IsFitted) return raw;
        var h = Map(0, raw.Home);
        var d = Map(1, raw.Draw);
        var a = Map(2, raw.Away);
        // ProbTriple floors each value and divides by the sum: step 4 of the doc comment
        return new ProbTriple(h, d, a, Numerics.ProbabilityFloor);
    }

    public string DescribeParameters() =>
        $"isotonic blocks: home {_value[0]?.Length ?? 0}, draw {_value[1]?.Length ?? 0}, away {_value[2]?.Length ?? 0} " +
        "(fitted by PAVA; no tunable knob)";

    public string ParametersJson()
    {
        static string F(double v) => v.ToString("0.000000", CultureInfo.InvariantCulture);
        var names = new[] { "home", "draw", "away" };
        var parts = new List<string>();
        for (var k = 0; k < 3; k++)
        {
            if (_value[k] is null) { parts.Add($"\"{names[k]}\": []"); continue; }
            var blocks = new List<string>();
            for (var b = 0; b < _value[k].Length; b++)
                blocks.Add($"{{ \"rawFrom\": {F(_xLow[k][b])}, \"rawTo\": {F(_xHigh[k][b])}, \"calibrated\": {F(_value[k][b])} }}");
            parts.Add($"\"{names[k]}\": [{string.Join(", ", blocks)}]");
        }
        return "\"parameters\": { " + string.Join(", ", parts) + " }";
    }

    public ICalibrator FreshCopy() => new IsotonicOvr();
}
