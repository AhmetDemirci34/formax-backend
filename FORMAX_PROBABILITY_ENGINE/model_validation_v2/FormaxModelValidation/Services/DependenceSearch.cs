using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;

namespace Formax.ModelValidation.Services;

public sealed class DependencePoint
{
    public required double Value { get; init; }
    /// <summary>1X2 log loss on VALIDATION - the selection criterion.</summary>
    public required double ValidationLogLoss { get; init; }
    public required double ValidationBrier { get; init; }
    public required double ValidationRps { get; init; }
    public required double ValidationAccuracy { get; init; }
    public required int ValidationN { get; init; }
    /// <summary>Mean log P(exact score) on TRAIN - the maximum likelihood criterion, computed on a different segment.</summary>
    public required double TrainScoreLogLik { get; init; }
    public required int TrainN { get; init; }
}

/// <summary>
/// Sweeps the two dependence parameters - Dixon-Coles rho and the bivariate shared component -
/// over the cached lambdas.
///
/// Nothing here rebuilds the rating: the lambdas do not depend on rho or on c, so one replay is
/// enough and every point of the sweep sees exactly the same pre-match state. Both criteria are
/// computed for every point:
///
///   * VALIDATION 1X2 log loss - the metric the models are judged on, on held-out matches
///   * TRAIN exact-score log likelihood - the classical maximum likelihood estimate, on the
///     earlier segment only
///
/// The first is the selection rule declared in the split config. The second is reported next to it
/// so a disagreement between "what the literature fits" and "what generalises here" is visible
/// rather than hidden. Neither one can see a single TEST match: the cache is built from the
/// fenced match list.
/// </summary>
public static class DependenceSearch
{
    private static DixonColesConfig Variant(DixonColesConfig cfg, double rho) => new()
    {
        Rho = rho,
        MaxGoals = cfg.MaxGoals,
        MinLambda = cfg.MinLambda,
        MaxLambda = cfg.MaxLambda,
        ProbabilityFloor = cfg.ProbabilityFloor
    };

    public static List<DependencePoint> SweepRho(
        IReadOnlyList<LambdaSample> cache, DixonColesConfig cfg, IEnumerable<double> grid)
    {
        var points = grid.ToList();
        var outp = new DependencePoint[points.Count];

        Parallel.For(0, points.Count, i =>
        {
            var rho = points[i];
            var c = Variant(cfg, rho);
            var val = new MetricAccumulator("DIXON_COLES", "VALIDATION", "rho");
            double trainLL = 0; var trainN = 0;

            foreach (var s in cache)
            {
                if (s.Seg == Segment.Validation)
                    val.Add(DixonColesModel.Outcome1X2(s.Lh, s.La, c), s.Actual);
                else if (s.Seg == Segment.Train)
                {
                    var g = DixonColesModel.ScoreGrid(s.Lh, s.La, c);
                    var x = Math.Min(s.Hg, cfg.MaxGoals);
                    var y = Math.Min(s.Ag, cfg.MaxGoals);
                    trainLL += Math.Log(Math.Max(g[x][y], 1e-300));
                    trainN++;
                }
            }

            outp[i] = new DependencePoint
            {
                Value = rho,
                ValidationLogLoss = val.LogLoss,
                ValidationBrier = val.Brier,
                ValidationRps = val.Rps,
                ValidationAccuracy = val.Accuracy,
                ValidationN = val.N,
                TrainScoreLogLik = trainN == 0 ? double.NaN : trainLL / trainN,
                TrainN = trainN
            };
        });

        return outp.ToList();
    }

    public static List<DependencePoint> SweepBivariate(
        IReadOnlyList<LambdaSample> cache, DixonColesConfig cfg, BivariateMode mode, IEnumerable<double> grid)
    {
        var points = grid.ToList();
        var outp = new DependencePoint[points.Count];

        Parallel.For(0, points.Count, i =>
        {
            var cVal = points[i];
            var val = new MetricAccumulator("BIVARIATE_POISSON", "VALIDATION", mode.ToString());
            double trainLL = 0; var trainN = 0;

            foreach (var s in cache)
            {
                var l3 = BivariatePoissonModel.SharedComponent(s.Lh, s.La, mode, cVal);
                if (s.Seg == Segment.Validation)
                    val.Add(BivariatePoissonModel.Outcome1X2(s.Lh, s.La, l3, cfg), s.Actual);
                else if (s.Seg == Segment.Train)
                {
                    trainLL += BivariatePoissonModel.LogScoreLikelihood(s.Lh, s.La, l3, s.Hg, s.Ag, cfg);
                    trainN++;
                }
            }

            outp[i] = new DependencePoint
            {
                Value = cVal,
                ValidationLogLoss = val.LogLoss,
                ValidationBrier = val.Brier,
                ValidationRps = val.Rps,
                ValidationAccuracy = val.Accuracy,
                ValidationN = val.N,
                TrainScoreLogLik = trainN == 0 ? double.NaN : trainLL / trainN,
                TrainN = trainN
            };
        });

        return outp.ToList();
    }

    public static IEnumerable<double> Grid(double from, double to, double step)
    {
        var n = (int)Math.Round((to - from) / step);
        for (var i = 0; i <= n; i++) yield return Math.Round(from + i * step, 6);
    }
}
