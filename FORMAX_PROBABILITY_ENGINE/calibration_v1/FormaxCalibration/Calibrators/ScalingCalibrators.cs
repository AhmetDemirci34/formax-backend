using System.Globalization;
using Formax.Calibration.Models;
using Formax.Calibration.Services;
using Formax.DixonColes.Models;

namespace Formax.Calibration.Calibrators;

/// <summary>
/// Temperature scaling: p'_k proportional to p_k^(1/T).
///
/// One parameter. T &gt; 1 flattens the distribution (less confident), T &lt; 1 sharpens it (more
/// confident), T = 1 is exactly the raw model. It cannot move probability mass between classes in
/// any other way - it can only make the model more or less sure of what it already believed.
///
/// This is the smallest possible answer to the V1 finding that the model was systematically
/// under-confident, which is why it is here despite being the simplest method in the set.
/// </summary>
public sealed class TemperatureScaling : ICalibrator
{
    private double _t = 1.0;
    public string Name => "TEMPERATURE_SCALING";
    public int ParameterCount => 1;
    public bool IsFitted { get; private set; }
    public double Temperature => _t;
    /// <summary>Mean negative log likelihood at the chosen T, on the data it was fitted to.</summary>
    public double FinalLoss { get; private set; }

    public void Fit(IReadOnlyList<CalibrationSample> history)
    {
        if (history.Count == 0) { _t = 1.0; IsFitted = false; return; }

        var z = new double[history.Count * 3];
        var y = new int[history.Count];
        for (var i = 0; i < history.Count; i++)
        {
            var p = history[i].Raw;
            z[i * 3 + 0] = Numerics.SafeLog(p.Home);
            z[i * 3 + 1] = Numerics.SafeLog(p.Draw);
            z[i * 3 + 2] = Numerics.SafeLog(p.Away);
            y[i] = (int)history[i].Actual;
        }

        double Nll(double t)
        {
            if (t <= 0) return double.PositiveInfinity;
            var inv = 1.0 / t;
            var sum = 0.0;
            Span<double> l = stackalloc double[3];
            for (var i = 0; i < y.Length; i++)
            {
                l[0] = z[i * 3] * inv; l[1] = z[i * 3 + 1] * inv; l[2] = z[i * 3 + 2] * inv;
                Numerics.Softmax(l);
                sum += -Numerics.SafeLog(l[y[i]]);
            }
            return sum / y.Length;
        }

        _t = Numerics.GoldenSection(Nll, 0.05, 20.0);
        FinalLoss = Nll(_t);
        IsFitted = true;
    }

    public ProbTriple Apply(ProbTriple raw)
    {
        if (!IsFitted) return raw;
        var inv = 1.0 / _t;
        Span<double> l = stackalloc double[3]
        {
            Numerics.SafeLog(raw.Home) * inv,
            Numerics.SafeLog(raw.Draw) * inv,
            Numerics.SafeLog(raw.Away) * inv
        };
        Numerics.Softmax(l);
        return new ProbTriple(l[0], l[1], l[2], Numerics.ProbabilityFloor);
    }

    public string DescribeParameters() =>
        $"T = {_t.ToString("0.000000", CultureInfo.InvariantCulture)} " +
        (_t < 1 ? "(sharpens: the raw model was under-confident)" : _t > 1 ? "(flattens: the raw model was over-confident)" : "(identity)");

    public string ParametersJson() =>
        $"\"parameters\": {{ \"T\": {_t.ToString("0.00000000", CultureInfo.InvariantCulture)} }}";

    public ICalibrator FreshCopy() => new TemperatureScaling();
}

/// <summary>
/// Matrix scaling, i.e. multinomial logistic calibration:
///
///   z = log(p_raw),   p' = softmax(W z + b)
///
/// With W free (3x3) this is multinomial logistic regression on the log raw probabilities:
/// 12 parameters, and the negative log likelihood is convex in them. With W constrained to be
/// diagonal it is vector scaling (6 parameters), and with W = (1/T)I and b = 0 it is temperature
/// scaling. The three methods in this file are therefore nested, which is what makes the
/// comparison between them meaningful rather than a beauty contest between unrelated formulas.
///
/// The fit starts at W = I, b = 0 - exactly the raw model - so the optimiser begins from "no
/// calibration" and any movement away from it is something the training data actually asked for.
/// </summary>
public sealed class MatrixScaling : ICalibrator
{
    private readonly bool _diagonal;
    /// <summary>
    /// Laid out per class so both variants share one solver: theta[k*d .. k*d+d) are the weights of
    /// class k. Diagonal: d = 2, features [z_k, 1]. Full: d = 4, features [z_H, z_D, z_A, 1].
    /// </summary>
    private double[] _theta;
    private int Dim => _diagonal ? 2 : 4;

    public string Name => _diagonal ? "VECTOR_SCALING" : "MULTINOMIAL_LOGISTIC";
    public int ParameterCount => _diagonal ? 6 : 12;
    public bool IsFitted { get; private set; }
    public NewtonSolver.Result? Report { get; private set; }

    public MatrixScaling(bool diagonal)
    {
        _diagonal = diagonal;
        _theta = Identity();
    }

    /// <summary>The identity transform: the fit starts at exactly the raw model.</summary>
    private double[] Identity()
    {
        if (_diagonal) return new[] { 1.0, 0.0, 1.0, 0.0, 1.0, 0.0 };          // (a_k, b_k) per class
        return new[]
        {
            1.0, 0.0, 0.0, 0.0,   // class H: 1*z_H
            0.0, 1.0, 0.0, 0.0,   // class D: 1*z_D
            0.0, 0.0, 1.0, 0.0    // class A: 1*z_A
        };
    }

    private void Features(ReadOnlySpan<double> z, Span<double> phi)
    {
        if (_diagonal)
            for (var k = 0; k < 3; k++) { phi[k * 2] = z[k]; phi[k * 2 + 1] = 1.0; }
        else
            for (var k = 0; k < 3; k++)
            { phi[k * 4] = z[0]; phi[k * 4 + 1] = z[1]; phi[k * 4 + 2] = z[2]; phi[k * 4 + 3] = 1.0; }
    }

    private void Logits(ReadOnlySpan<double> z, Span<double> l)
    {
        Span<double> phi = stackalloc double[3 * Dim];
        Features(z, phi);
        var d = Dim;
        for (var k = 0; k < 3; k++)
        {
            var s = 0.0;
            for (var f = 0; f < d; f++) s += _theta[k * d + f] * phi[k * d + f];
            l[k] = s;
        }
    }

    public void Fit(IReadOnlyList<CalibrationSample> history)
    {
        _theta = Identity();
        if (history.Count == 0) { IsFitted = false; return; }

        var n = history.Count;
        var z = new double[n * 3];
        var y = new int[n];
        for (var i = 0; i < n; i++)
        {
            var p = history[i].Raw;
            z[i * 3] = Numerics.SafeLog(p.Home);
            z[i * 3 + 1] = Numerics.SafeLog(p.Draw);
            z[i * 3 + 2] = Numerics.SafeLog(p.Away);
            y[i] = (int)history[i].Actual;
        }

        var diagonal = _diagonal;
        var d = Dim;

        void Features(int i, double[] phi)
        {
            var z0 = z[i * 3]; var z1 = z[i * 3 + 1]; var z2 = z[i * 3 + 2];
            if (diagonal)
            {
                phi[0] = z0; phi[1] = 1.0;
                phi[2] = z1; phi[3] = 1.0;
                phi[4] = z2; phi[5] = 1.0;
            }
            else
                for (var k = 0; k < 3; k++)
                { phi[k * 4] = z0; phi[k * 4 + 1] = z1; phi[k * 4 + 2] = z2; phi[k * 4 + 3] = 1.0; }
        }

        Report = NewtonSolver.Fit(_theta, classes: 3, featureDim: d, sampleCount: n,
            features: Features, label: i => y[i]);
        IsFitted = true;
    }

    public ProbTriple Apply(ProbTriple raw)
    {
        if (!IsFitted) return raw;
        Span<double> z = stackalloc double[3]
        { Numerics.SafeLog(raw.Home), Numerics.SafeLog(raw.Draw), Numerics.SafeLog(raw.Away) };
        Span<double> l = stackalloc double[3];
        Logits(z, l);
        Numerics.Softmax(l);
        return new ProbTriple(l[0], l[1], l[2], Numerics.ProbabilityFloor);
    }

    public string DescribeParameters()
    {
        static string F(double v) => v.ToString("+0.0000;-0.0000", CultureInfo.InvariantCulture);
        if (_diagonal)
            return $"a = [{F(_theta[0])}, {F(_theta[2])}, {F(_theta[4])}]  b = [{F(_theta[1])}, {F(_theta[3])}, {F(_theta[5])}]";
        return $"W = [[{F(_theta[0])},{F(_theta[1])},{F(_theta[2])}], [{F(_theta[4])},{F(_theta[5])},{F(_theta[6])}], " +
               $"[{F(_theta[8])},{F(_theta[9])},{F(_theta[10])}]]  b = [{F(_theta[3])},{F(_theta[7])},{F(_theta[11])}]";
    }

    public string ParametersJson()
    {
        static string F(double v) => v.ToString("0.00000000", CultureInfo.InvariantCulture);
        if (_diagonal)
            return $"\"parameters\": {{ \"a\": [{F(_theta[0])}, {F(_theta[2])}, {F(_theta[4])}], " +
                   $"\"b\": [{F(_theta[1])}, {F(_theta[3])}, {F(_theta[5])}] }}";
        return "\"parameters\": { \"W\": [" +
               $"[{F(_theta[0])}, {F(_theta[1])}, {F(_theta[2])}], " +
               $"[{F(_theta[4])}, {F(_theta[5])}, {F(_theta[6])}], " +
               $"[{F(_theta[8])}, {F(_theta[9])}, {F(_theta[10])}]], " +
               $"\"b\": [{F(_theta[3])}, {F(_theta[7])}, {F(_theta[11])}] }}";
    }

    public ICalibrator FreshCopy() => new MatrixScaling(_diagonal);
}
