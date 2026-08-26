namespace Formax.ModelValidation.Config;

/// <summary>
/// The candidate values, written down in one place so the search space is auditable.
///
/// The ranges were chosen to BRACKET the unvalidated V1 defaults (365 / 0.08 / 6 / 0.60) on both
/// sides with room to spare, before any result was seen. If a selected value lands on the edge of
/// a range that is a finding in itself and is reported as such - a parameter search that stops at
/// its own boundary has not converged.
/// </summary>
public static class SearchGrid
{
    // ---- primary: swept as a full three-dimensional grid
    /// <summary>The last value is not a half-life anyone would set: it is 274 years, i.e. NO time decay at all,
    /// included so the search can answer "does decay help here?" instead of only "how much decay?".</summary>
    public static readonly double[] HalfLifeDays =
        { 60, 90, 120, 180, 270, 365, 500, 730, 1095, 1460, 2000, 2920, 4380, 100000 };

    public static readonly double[] LearningRate =
        { 0.01, 0.02, 0.03, 0.04, 0.06, 0.08, 0.12, 0.16, 0.22, 0.30, 0.40, 0.55 };

    /// <summary>0 means no pooling at all: a team's own evidence carries full weight from its first match.</summary>
    public static readonly double[] ShrinkageK =
        { 0.0, 0.1, 0.25, 0.5, 1, 2, 3, 4, 6, 9, 14, 20, 30 };

    // ---- secondary: swept one coordinate at a time, at the current best primary values
    public static readonly double[] RatioSmoothing =
        { 0.05, 0.10, 0.20, 0.30, 0.45, 0.60, 0.80, 1.00, 1.25, 1.50, 2.00, 3.00, 4.00, 6.00 };

    public static readonly int[] MinBaselineSamples = { 5, 10, 20, 35, 50, 100, 200 };

    public static readonly bool[] UseCompetitionTypePool = { true, false };

    /// <summary>Index guard rails, swept as a pair because a lower bound only means anything next to its upper bound.</summary>
    public static readonly (double min, double max)[] IndexClamp =
        { (0.02, 50.0), (0.05, 20.0), (0.10, 10.0), (0.15, 6.0), (0.25, 4.0), (0.35, 3.0), (0.50, 2.0) };

    // ---- dependence parameters, swept over cached lambdas
    public const double RhoFrom = -0.30, RhoTo = 0.30, RhoStep = 0.01;
    public const double BivariateProportionalFrom = 0.00, BivariateProportionalTo = 0.90, BivariateProportionalStep = 0.02;
    public const double BivariateConstantFrom = 0.00, BivariateConstantTo = 1.20, BivariateConstantStep = 0.02;

    /// <summary>How many times the primary grid is re-run after the secondary coordinates move.</summary>
    public const int MaxRounds = 6;

    public static int PrimaryGridSize => HalfLifeDays.Length * LearningRate.Length * ShrinkageK.Length;

    /// <summary>
    /// Reports every selected value that sits on the edge of its own candidate range. A search that
    /// stops at its own boundary has not found an optimum, it has run out of room - and the honest
    /// thing to do is say so rather than publish the edge as if it were a result.
    /// </summary>
    /// <summary>
    /// An edge is only a problem when the search ran out of ROOM. Some of these ranges end at a
    /// limiting case instead - no decay, no pooling, no clamp - where there is nothing beyond the
    /// last value to search. The two are reported differently because they mean different things:
    /// one says "extend the grid", the other says "the mechanism is simply switched off".
    /// </summary>
    public static List<string> ValuesOnGridEdge(
        double halfLife, double learningRate, double shrinkageK, double ratioSmoothing,
        int minBaselineSamples, double minIndex, double maxIndex)
    {
        var edges = new List<string>();
        void Check(string name, double v, IReadOnlyList<double> grid, string? lowLimit = null, string? highLimit = null)
        {
            if (Math.Abs(v - grid[0]) < 1e-12)
                edges.Add(lowLimit is null
                    ? $"{name}={v} lowest candidate - the optimum may lie below the search space"
                    : $"{name}={v} {lowLimit} - a limiting case, nothing to search beyond it");
            else if (Math.Abs(v - grid[^1]) < 1e-12)
                edges.Add(highLimit is null
                    ? $"{name}={v} highest candidate - the optimum may lie above the search space"
                    : $"{name}={v} {highLimit} - a limiting case, nothing to search beyond it");
        }

        Check("halfLifeDays", halfLife, HalfLifeDays, highLimit: "(no time decay)");
        Check("learningRate", learningRate, LearningRate);
        Check("shrinkageK", shrinkageK, ShrinkageK, lowLimit: "(no partial pooling)");
        Check("ratioSmoothing", ratioSmoothing, RatioSmoothing);
        Check("minBaselineSamples", minBaselineSamples, MinBaselineSamples.Select(v => (double)v).ToList());
        if (Math.Abs(maxIndex - IndexClamp[0].max) < 1e-12)
            edges.Add($"indexClamp=[{minIndex},{maxIndex}] (effectively unclamped) - a limiting case, nothing to search beyond it");
        else if (Math.Abs(maxIndex - IndexClamp[^1].max) < 1e-12)
            edges.Add($"indexClamp=[{minIndex},{maxIndex}] narrowest candidate - the optimum may lie below the search space");
        return edges;
    }
}
