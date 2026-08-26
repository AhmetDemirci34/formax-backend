using System.Globalization;

namespace Formax.GatePolicy.Services;

/// <summary>
/// Does the gate refuse the RIGHT matches, or does it just refuse matches?
///
/// A selective classifier that drops predictions will usually show a lower average loss on what it
/// kept - but so would dropping a random subset of the same size, purely because averages move.
/// (In expectation random dropping leaves the mean exactly where it was, so any systematic shift is
/// real selection; the point of this test is to measure whether the observed shift is bigger than
/// the SAMPLING NOISE of dropping that many matches.)
///
/// So: hold the number of refusals fixed, refuse that many matches at random B times, and ask where
/// the real gate's published loss falls in that null distribution. A gate that is genuinely finding
/// the matches the model handles badly lands in the far left tail. A gate that is refusing matches
/// the model was fine with lands in the right tail - and is destroying value while looking careful.
///
/// Deterministic: draw b uses seed baseSeed + b.
/// </summary>
public static class SelectionTest
{
    public const int DefaultDraws = 2000;
    public const int DefaultSeed = 20260821;

    public sealed class Result
    {
        public required string Policy { get; init; }
        public required string Segment { get; init; }
        public required int Total { get; init; }
        public required int Published { get; init; }
        public required double ActualPublishedLogLoss { get; init; }
        /// <summary>Mean published log loss when the same NUMBER of matches is refused at random.</summary>
        public required double NullMean { get; init; }
        public required double NullP025 { get; init; }
        public required double NullP975 { get; init; }
        /// <summary>Share of random refusals that did at least as well as the real gate. Small = the gate is selecting.</summary>
        public required double PValue { get; init; }
        public required int Draws { get; init; }

        /// <summary>Mean log loss the model had on the matches this gate refused.</summary>
        public required double ActualRejectedLogLoss { get; init; }
        /// <summary>Where a random refusal of the same size would have landed.</summary>
        public required double RejectedNullP025 { get; init; }
        public required double RejectedNullP975 { get; init; }

        /// <summary>
        /// The refused matches really were harder than average - even if that did not move the
        /// aggregate enough to register. Separates "the gate picks the wrong matches" from "the
        /// gate picks the right matches but too few of them to measure".
        /// </summary>
        public bool RefusedMatchesWereHarder => ActualRejectedLogLoss > RejectedNullP975;

        public string Verdict =>
            Published == Total ? "NO REFUSALS - nothing to test"
            : PValue < 0.05 ? "SELECTS BETTER THAN CHANCE"
            : PValue > 0.95 ? "WORSE THAN CHANCE - refusing matches the model handled well"
            : RefusedMatchesWereHarder ? "REFUSES HARDER MATCHES, TOO FEW TO MOVE THE AGGREGATE"
            : "INDISTINGUISHABLE FROM RANDOM REFUSAL";

        private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);

        public static string CsvHeader =>
            "Policy,Segment,Total,Published,Rejected,CoverageRate,ActualPublishedLogLoss," +
            "RandomKeepMean,RandomKeepP025,RandomKeepP975,PValue," +
            "ActualRejectedLogLoss,RandomRejectP025,RandomRejectP975,RefusedMatchesWereHarder,Draws,Verdict";

        public string ToCsv() => string.Join(',', Policy, Segment,
            Total.ToString(CultureInfo.InvariantCulture),
            Published.ToString(CultureInfo.InvariantCulture),
            (Total - Published).ToString(CultureInfo.InvariantCulture),
            F(Total == 0 ? double.NaN : (double)Published / Total),
            F(ActualPublishedLogLoss), F(NullMean), F(NullP025), F(NullP975), F(PValue),
            F(ActualRejectedLogLoss), F(RejectedNullP025), F(RejectedNullP975),
            RefusedMatchesWereHarder ? "True" : "False",
            Draws.ToString(CultureInfo.InvariantCulture),
            "\"" + Verdict + "\"");
    }

    public static Result Run(IReadOnlyList<double> losses, bool[] published, string policy, string segment,
        int draws = DefaultDraws, int seed = DefaultSeed)
    {
        var n = losses.Count;
        var k = 0;
        var actual = 0.0;
        for (var i = 0; i < n; i++) if (published[i]) { actual += losses[i]; k++; }
        actual = k == 0 ? double.NaN : actual / k;

        var rejectedCount = n - k;
        var actualRejected = 0.0;
        for (var i = 0; i < n; i++) if (!published[i]) actualRejected += losses[i];
        actualRejected = rejectedCount == 0 ? double.NaN : actualRejected / rejectedCount;

        if (k == n || k == 0)
            return new Result
            {
                Policy = policy, Segment = segment, Total = n, Published = k,
                ActualPublishedLogLoss = actual, NullMean = actual,
                NullP025 = actual, NullP975 = actual, PValue = double.NaN, Draws = 0,
                ActualRejectedLogLoss = actualRejected,
                RejectedNullP025 = double.NaN, RejectedNullP975 = double.NaN
            };

        var all = losses.ToArray();
        var total = all.Sum();
        var nulls = new double[draws];
        var rejectedNulls = new double[draws];
        Parallel.For(0, draws, b =>
        {
            var rng = new Random(seed + b);
            var idx = new int[n];
            for (var i = 0; i < n; i++) idx[i] = i;
            // partial Fisher-Yates: the first k entries become a uniform random size-k subset
            for (var i = 0; i < k; i++)
            {
                var j = i + rng.Next(n - i);
                (idx[i], idx[j]) = (idx[j], idx[i]);
            }
            var sum = 0.0;
            for (var i = 0; i < k; i++) sum += all[idx[i]];
            nulls[b] = sum / k;
            rejectedNulls[b] = (total - sum) / rejectedCount;
        });

        var atLeastAsGood = nulls.Count(v => v <= actual);
        Array.Sort(nulls);
        Array.Sort(rejectedNulls);

        return new Result
        {
            Policy = policy,
            Segment = segment,
            Total = n,
            Published = k,
            ActualPublishedLogLoss = actual,
            NullMean = nulls.Average(),
            NullP025 = Percentile(nulls, 0.025),
            NullP975 = Percentile(nulls, 0.975),
            PValue = (double)atLeastAsGood / draws,
            Draws = draws,
            ActualRejectedLogLoss = actualRejected,
            RejectedNullP025 = Percentile(rejectedNulls, 0.025),
            RejectedNullP975 = Percentile(rejectedNulls, 0.975)
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
