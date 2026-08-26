namespace Formax.Calibration.Services;

/// <summary>Small, dependency-free numerical helpers. Deterministic: no randomness anywhere.</summary>
public static class Numerics
{
    /// <summary>Probability floor shared by the raw pipeline and every calibrator, so no method wins on a different floor.</summary>
    public const double ProbabilityFloor = 1e-6;

    public static double SafeLog(double p) => Math.Log(Math.Max(p, 1e-12));

    /// <summary>Numerically stable softmax, in place.</summary>
    public static void Softmax(Span<double> logits)
    {
        var max = double.NegativeInfinity;
        foreach (var l in logits) if (l > max) max = l;
        var sum = 0.0;
        for (var i = 0; i < logits.Length; i++) { logits[i] = Math.Exp(logits[i] - max); sum += logits[i]; }
        for (var i = 0; i < logits.Length; i++) logits[i] /= sum;
    }

    /// <summary>
    /// Golden-section minimisation of a unimodal function on [lo, hi]. Used for the single
    /// temperature parameter, where a 1-D search is more reliable than a gradient method.
    /// </summary>
    public static double GoldenSection(Func<double, double> f, double lo, double hi, double tolerance = 1e-10)
    {
        const double invPhi = 0.6180339887498949;
        var a = lo; var b = hi;
        var c = b - (b - a) * invPhi;
        var d = a + (b - a) * invPhi;
        var fc = f(c); var fd = f(d);
        for (var i = 0; i < 300 && Math.Abs(b - a) > tolerance; i++)
        {
            if (fc < fd) { b = d; d = c; fd = fc; c = b - (b - a) * invPhi; fc = f(c); }
            else { a = c; c = d; fc = fd; d = a + (b - a) * invPhi; fd = f(d); }
        }
        return (a + b) / 2.0;
    }

    /// <summary>
    /// Pool Adjacent Violators. Returns the isotonic (non-decreasing) fit of y against sorted x,
    /// as blocks: the value of block b applies to every x in [xLow[b], xHigh[b]].
    /// Parameter free - there is no bin count, no smoothing constant, nothing to tune.
    /// </summary>
    public static (double[] xLow, double[] xHigh, double[] value) Pava(double[] x, double[] y)
    {
        var n = x.Length;
        if (n == 0) return (Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());

        var idx = Enumerable.Range(0, n).ToArray();
        Array.Sort(idx, (a, b) => x[a] != x[b] ? x[a].CompareTo(x[b]) : a.CompareTo(b));

        var sumY = new double[n];
        var count = new double[n];
        var lo = new double[n];
        var hi = new double[n];
        var blocks = 0;

        foreach (var i in idx)
        {
            sumY[blocks] = y[i]; count[blocks] = 1; lo[blocks] = x[i]; hi[blocks] = x[i];
            blocks++;
            while (blocks > 1 && sumY[blocks - 2] / count[blocks - 2] > sumY[blocks - 1] / count[blocks - 1])
            {
                sumY[blocks - 2] += sumY[blocks - 1];
                count[blocks - 2] += count[blocks - 1];
                hi[blocks - 2] = hi[blocks - 1];
                blocks--;
            }
        }

        var xl = new double[blocks];
        var xh = new double[blocks];
        var v = new double[blocks];
        for (var b = 0; b < blocks; b++) { xl[b] = lo[b]; xh[b] = hi[b]; v[b] = sumY[b] / count[b]; }
        return (xl, xh, v);
    }
}
