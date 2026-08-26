using Formax.DixonColes.Config;
using Formax.DixonColes.Models;

namespace Formax.DixonColes.Services;

/// <summary>
/// Dixon-Coles score grid.
///
///   P(x,y) = tau(x,y,lambda,mu,rho) * Poisson(x;lambda) * Poisson(y;mu)
///
///   tau = 1 - lambda*mu*rho   (0,0)
///         1 + lambda*rho      (0,1)
///         1 + mu*rho          (1,0)
///         1 - rho             (1,1)
///         1                   otherwise
///
/// The grid is truncated at MaxGoals and then renormalised, so the reported 1X2 probabilities sum
/// to exactly 1 (verified in tests to 1e-12).
///
/// rho is taken from config as a CONSTANT. It is NOT fitted on data in this phase - that is what
/// makes this a baseline and why every result below must be read as UNVALIDATED.
/// </summary>
public static class DixonColesModel
{
    public static double Tau(int x, int y, double lambda, double mu, double rho)
    {
        if (x == 0 && y == 0) return 1.0 - lambda * mu * rho;
        if (x == 0 && y == 1) return 1.0 + lambda * rho;
        if (x == 1 && y == 0) return 1.0 + mu * rho;
        if (x == 1 && y == 1) return 1.0 - rho;
        return 1.0;
    }

    private static double[] PoissonPmf(double lambda, int maxGoals)
    {
        var p = new double[maxGoals + 1];
        var term = Math.Exp(-lambda);
        p[0] = term;
        for (var k = 1; k <= maxGoals; k++)
        {
            term = term * lambda / k;
            p[k] = term;
        }
        return p;
    }

    /// <summary>Full normalised score grid. grid[x][y] = P(home x, away y).</summary>
    public static double[][] ScoreGrid(double lambda, double mu, DixonColesConfig cfg)
    {
        var n = cfg.MaxGoals;
        var ph = PoissonPmf(lambda, n);
        var pa = PoissonPmf(mu, n);
        var grid = new double[n + 1][];
        var total = 0.0;

        for (var x = 0; x <= n; x++)
        {
            grid[x] = new double[n + 1];
            for (var y = 0; y <= n; y++)
            {
                var v = Tau(x, y, lambda, mu, cfg.Rho) * ph[x] * pa[y];
                if (v < 0) v = 0;                      // tau can go negative for extreme rho
                grid[x][y] = v;
                total += v;
            }
        }
        if (total <= 0) throw new InvalidOperationException("degenerate score grid");
        for (var x = 0; x <= n; x++)
            for (var y = 0; y <= n; y++)
                grid[x][y] /= total;

        return grid;
    }

    public static ProbTriple Outcome1X2(double lambda, double mu, DixonColesConfig cfg)
    {
        var grid = ScoreGrid(lambda, mu, cfg);
        double h = 0, d = 0, a = 0;
        for (var x = 0; x < grid.Length; x++)
            for (var y = 0; y < grid[x].Length; y++)
            {
                if (x > y) h += grid[x][y];
                else if (x == y) d += grid[x][y];
                else a += grid[x][y];
            }
        return new ProbTriple(h, d, a, cfg.ProbabilityFloor);
    }

    /// <summary>Same grid with rho forced to 0: plain independent Poisson, used as a reference model.</summary>
    public static ProbTriple Outcome1X2IndependentPoisson(double lambda, double mu, DixonColesConfig cfg)
    {
        var zeroRho = new DixonColesConfig
        {
            Rho = 0.0,
            MaxGoals = cfg.MaxGoals,
            MinLambda = cfg.MinLambda,
            MaxLambda = cfg.MaxLambda,
            ProbabilityFloor = cfg.ProbabilityFloor
        };
        return Outcome1X2(lambda, mu, zeroRho);
    }
}
