namespace Formax.Calibration.Services;

/// <summary>
/// Damped Newton solver for the calibration fits.
///
/// Both scaling calibrators are generalised linear models: the logit of class k is a LINEAR
/// function of that class's feature vector, so the negative log likelihood is convex and its
/// Hessian is available in closed form. With only 6 or 12 parameters, Newton converges in about ten
/// passes over the data and drives the gradient to the limits of double precision - a first-order
/// method needs thousands of passes and still plateaus around 1e-5, which is not a converged fit
/// and should not be reported as one.
///
/// The softmax is shift invariant, so the Hessian is singular by construction (adding a constant to
/// every logit changes nothing). A small ridge makes the linear system solvable without moving the
/// optimum in any direction that matters: it only pins down the otherwise-free direction.
/// </summary>
public static class NewtonSolver
{
    public const double Ridge = 1e-9;

    public sealed class Result
    {
        public required int Iterations { get; init; }
        public required double FinalLoss { get; init; }
        public required double FinalGradientNorm { get; init; }
        public required bool Converged { get; init; }
    }

    /// <summary>
    /// Fits theta for a model whose logit of class k is theta[k] . phi[k].
    /// <paramref name="features"/> writes the per-class feature vectors of sample i.
    /// </summary>
    public static Result Fit(
        double[] theta, int classes, int featureDim, int sampleCount,
        Action<int, double[]> features,      // (sampleIndex, out featureBuffer[classes * featureDim])
        Func<int, int> label,                // sampleIndex -> class
        int maxIterations = 60, double tolerance = 1e-12)
    {
        var p = classes * featureDim;
        var phi = new double[p];
        var grad = new double[p];
        var hess = new double[p * p];
        var q = new double[classes];
        var logits = new double[classes];

        double LossAndDerivatives(double[] th, double[]? g, double[]? h)
        {
            if (g is not null) Array.Clear(g);
            if (h is not null) Array.Clear(h);
            var loss = 0.0;

            for (var i = 0; i < sampleCount; i++)
            {
                features(i, phi);
                for (var k = 0; k < classes; k++)
                {
                    var s = 0.0;
                    for (var f = 0; f < featureDim; f++) s += th[k * featureDim + f] * phi[k * featureDim + f];
                    logits[k] = s;
                }
                logits.AsSpan().CopyTo(q);
                Numerics.Softmax(q);

                var y = label(i);
                loss += -Numerics.SafeLog(q[y]);

                if (g is null) continue;
                for (var k = 0; k < classes; k++)
                {
                    var d = q[k] - (k == y ? 1.0 : 0.0);
                    for (var f = 0; f < featureDim; f++) g[k * featureDim + f] += d * phi[k * featureDim + f];
                }

                if (h is null) continue;
                for (var k = 0; k < classes; k++)
                    for (var l = 0; l < classes; l++)
                    {
                        var w = q[k] * ((k == l ? 1.0 : 0.0) - q[l]);
                        if (w == 0) continue;
                        for (var a = 0; a < featureDim; a++)
                        {
                            var pa = w * phi[k * featureDim + a];
                            if (pa == 0) continue;
                            var rowBase = (k * featureDim + a) * p + l * featureDim;
                            for (var b = 0; b < featureDim; b++)
                                h[rowBase + b] += pa * phi[l * featureDim + b];
                        }
                    }
            }

            var n = sampleCount;
            if (g is not null) for (var j = 0; j < p; j++) g[j] /= n;
            if (h is not null) for (var j = 0; j < p * p; j++) h[j] /= n;
            return loss / n;
        }

        var loss = LossAndDerivatives(theta, grad, hess);
        var it = 0;
        var gradNorm = MaxAbs(grad);

        for (; it < maxIterations && gradNorm > tolerance; it++)
        {
            for (var j = 0; j < p; j++) hess[j * p + j] += Ridge;
            var step = SolveSymmetric(hess, grad, p);
            if (step is null) break;   // Hessian not usable: stop and report what we have

            // backtracking so a Newton step can never make things worse
            var alpha = 1.0;
            var candidate = new double[p];
            var accepted = false;
            for (var t = 0; t < 40; t++)
            {
                for (var j = 0; j < p; j++) candidate[j] = theta[j] - alpha * step[j];
                var newLoss = LossAndDerivatives(candidate, null, null);
                if (newLoss <= loss)
                {
                    candidate.CopyTo(theta, 0);
                    loss = newLoss;
                    accepted = true;
                    break;
                }
                alpha /= 2.0;
            }
            if (!accepted) break;

            loss = LossAndDerivatives(theta, grad, hess);
            var next = MaxAbs(grad);
            if (next >= gradNorm && next < 1e-10) { gradNorm = next; break; }
            gradNorm = next;
        }

        return new Result
        {
            Iterations = it,
            FinalLoss = loss,
            FinalGradientNorm = gradNorm,
            Converged = gradNorm <= tolerance * 1e3
        };
    }

    private static double MaxAbs(double[] v)
    {
        var m = 0.0;
        foreach (var x in v) m = Math.Max(m, Math.Abs(x));
        return m;
    }

    /// <summary>Cholesky solve of A x = b for symmetric positive definite A (row-major, n x n).</summary>
    private static double[]? SolveSymmetric(double[] a, double[] b, int n)
    {
        var l = new double[n * n];
        for (var i = 0; i < n; i++)
            for (var j = 0; j <= i; j++)
            {
                var sum = a[i * n + j];
                for (var k = 0; k < j; k++) sum -= l[i * n + k] * l[j * n + k];
                if (i == j)
                {
                    if (sum <= 0) return null;
                    l[i * n + j] = Math.Sqrt(sum);
                }
                else l[i * n + j] = sum / l[j * n + j];
            }

        var y = new double[n];
        for (var i = 0; i < n; i++)
        {
            var sum = b[i];
            for (var k = 0; k < i; k++) sum -= l[i * n + k] * y[k];
            y[i] = sum / l[i * n + i];
        }

        var x = new double[n];
        for (var i = n - 1; i >= 0; i--)
        {
            var sum = y[i];
            for (var k = i + 1; k < n; k++) sum -= l[k * n + i] * x[k];
            x[i] = sum / l[i * n + i];
        }
        return x;
    }
}
