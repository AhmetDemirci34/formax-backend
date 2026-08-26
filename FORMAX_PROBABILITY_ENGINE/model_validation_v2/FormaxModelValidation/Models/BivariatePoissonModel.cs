using Formax.DixonColes.Config;
using Formax.DixonColes.Models;

namespace Formax.ModelValidation.Models;

/// <summary>How the shared (covariance) component of the bivariate Poisson is set for a match.</summary>
public enum BivariateMode
{
    /// <summary>lambda3 = c * min(lambdaHome, lambdaAway). Dependence scales with the size of the match.</summary>
    Proportional,
    /// <summary>lambda3 = c goals, the same for every match (clamped so both marginals stay positive).</summary>
    Constant
}

/// <summary>
/// Bivariate Poisson, Karlis-Ntzoufras form:
///
///   X = W1 + W3,  Y = W2 + W3,  W1~P(l1), W2~P(l2), W3~P(l3), independent
///   E[X] = l1 + l3,  E[Y] = l2 + l3,  Cov(X,Y) = l3 >= 0
///
/// The marginal means are pinned to the SAME lambdas the other models use, so the only thing the
/// bivariate model adds is the dependence: l1 = lambdaHome - l3, l2 = lambdaAway - l3.
/// l3 = 0 collapses the model exactly onto independent Poisson - which is why "more complex"
/// cannot mean "better" here by construction: it can only win if the data actually carries
/// positive score dependence.
///
/// The pmf is evaluated in its convolution form
///
///   P(x,y) = e^-(l1+l2+l3) * SUM_k  l1^(x-k)/(x-k)! * l2^(y-k)/(y-k)! * l3^k/k!
///
/// which uses only small positive terms - the textbook (l3/(l1*l2))^k form overflows when the
/// lambdas are small, which they are for the clamped cold-start rows.
/// </summary>
public static class BivariatePoissonModel
{
    /// <summary>Upper bound on the shared component as a fraction of the smaller lambda, so both marginals stay strictly positive.</summary>
    public const double MaxShare = 0.95;

    public static double SharedComponent(double lambdaHome, double lambdaAway, BivariateMode mode, double c)
    {
        if (c <= 0) return 0.0;
        var cap = MaxShare * Math.Min(lambdaHome, lambdaAway);
        var l3 = mode == BivariateMode.Proportional ? c * Math.Min(lambdaHome, lambdaAway) : c;
        return Math.Min(l3, cap);
    }

    private static double[] PoissonTerms(double lambda, int n)
    {
        // t[i] = lambda^i / i!  (no exponential factor: it is applied once, outside)
        var t = new double[n + 1];
        t[0] = 1.0;
        for (var i = 1; i <= n; i++) t[i] = t[i - 1] * lambda / i;
        return t;
    }

    /// <summary>Normalised score grid. grid[x][y] = P(home x, away y).</summary>
    public static double[][] ScoreGrid(double lambdaHome, double lambdaAway, double lambda3, DixonColesConfig cfg)
    {
        var n = cfg.MaxGoals;
        var l1 = Math.Max(lambdaHome - lambda3, 1e-12);
        var l2 = Math.Max(lambdaAway - lambda3, 1e-12);
        var l3 = Math.Max(lambda3, 0.0);

        var a = PoissonTerms(l1, n);
        var b = PoissonTerms(l2, n);
        var c = PoissonTerms(l3, n);
        var e = Math.Exp(-(l1 + l2 + l3));

        var grid = new double[n + 1][];
        var total = 0.0;
        for (var x = 0; x <= n; x++)
        {
            grid[x] = new double[n + 1];
            for (var y = 0; y <= n; y++)
            {
                var s = 0.0;
                var kmax = Math.Min(x, y);
                for (var k = 0; k <= kmax; k++) s += a[x - k] * b[y - k] * c[k];
                var v = e * s;
                grid[x][y] = v;
                total += v;
            }
        }
        if (total <= 0) throw new InvalidOperationException("degenerate bivariate grid");
        for (var x = 0; x <= n; x++)
            for (var y = 0; y <= n; y++)
                grid[x][y] /= total;
        return grid;
    }

    public static ProbTriple Outcome1X2(double lambdaHome, double lambdaAway, double lambda3, DixonColesConfig cfg)
    {
        var g = ScoreGrid(lambdaHome, lambdaAway, lambda3, cfg);
        double h = 0, d = 0, aw = 0;
        for (var x = 0; x < g.Length; x++)
            for (var y = 0; y < g[x].Length; y++)
            {
                if (x > y) h += g[x][y];
                else if (x == y) d += g[x][y];
                else aw += g[x][y];
            }
        return new ProbTriple(h, d, aw, cfg.ProbabilityFloor);
    }

    /// <summary>Log P(x,y) under the model, truncated and renormalised exactly as the 1X2 grid is.</summary>
    public static double LogScoreLikelihood(double lambdaHome, double lambdaAway, double lambda3,
        int homeGoals, int awayGoals, DixonColesConfig cfg)
    {
        var g = ScoreGrid(lambdaHome, lambdaAway, lambda3, cfg);
        var x = Math.Min(homeGoals, cfg.MaxGoals);
        var y = Math.Min(awayGoals, cfg.MaxGoals);
        return Math.Log(Math.Max(g[x][y], 1e-300));
    }
}
