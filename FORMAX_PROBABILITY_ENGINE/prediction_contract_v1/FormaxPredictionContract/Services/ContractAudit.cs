using System.Globalization;
using Formax.Contract.Models;
using Formax.ModelValidation.Models;

namespace Formax.Contract.Services;

/// <summary>
/// Everything that must hold before the contract may be called ready. Each check is a measurement
/// with an expected value and an observed one; nothing is asserted on faith.
/// </summary>
public static class ContractAudit
{
    public static List<(string check, string expected, string observed, string status)> Run(
        IReadOnlyList<PredictionContract> contracts,
        IReadOnlyList<MatchPrediction> raws,
        PredictionStore store,
        bool regressionOk,
        double settledLogLoss,
        double contractLogLoss,
        PublishOutcome immutabilityProbe,
        PublishOutcome idempotentProbe)
    {
        var res = new List<(string, string, string, string)>();
        void Check(string name, string expected, string observed, bool ok)
            => res.Add((name, expected, observed, ok ? "PASS" : "FAIL"));

        // ---- 16. the contract reproduces the research model exactly
        Check("contract reproduces the model_validation_v2 raw probabilities on every segment",
            "MATCH", regressionOk ? "MATCH" : "DIFFERS", regressionOk);

        // ---- 1. probability shape
        var maxSumError = 0.0;
        var outOfRange = 0;
        double minP = 1.0, maxP = 0.0;
        foreach (var c in contracts.Where(c => c.PredictionEligible))
        {
            maxSumError = Math.Max(maxSumError, Math.Abs(c.ProbabilitySum - 1.0));
            foreach (var p in new[] { c.HomeProbability!.Value, c.DrawProbability!.Value, c.AwayProbability!.Value })
            {
                if (p < 0 || p > 1) outOfRange++;
                minP = Math.Min(minP, p);
                maxP = Math.Max(maxP, p);
            }
        }
        Check("probability sum error", "<= 1e-12",
            maxSumError.ToString("0.0e+0", CultureInfo.InvariantCulture), maxSumError <= 1e-12);
        Check("probabilities outside [0,1]", "0", outOfRange.ToString(), outOfRange == 0);
        Check("published probability range", "inside [0,1]",
            $"[{minP.ToString("0.000000", CultureInfo.InvariantCulture)}, {maxP.ToString("0.000000", CultureInfo.InvariantCulture)}]",
            minP >= 0 && maxP <= 1);

        // ---- 5. a rejected prediction carries no number
        var leaked = contracts.Count(c => !c.PredictionEligible &&
            (c.HomeProbability.HasValue || c.DrawProbability.HasValue || c.AwayProbability.HasValue));
        Check("rejected predictions that still carry a probability", "0", leaked.ToString(), leaked == 0);

        var statusMismatch = contracts.Count(c =>
            (c.PredictionEligible && c.GateStatus != GateStatus.Accepted) ||
            (!c.PredictionEligible && c.GateStatus != GateStatus.Rejected));
        Check("rows whose GateStatus disagrees with PredictionEligible", "0",
            statusMismatch.ToString(), statusMismatch == 0);

        // ---- 7. versioning is present and uniform
        var v = ContractVersions.Current;
        var badVersion = contracts.Count(c =>
            c.Versions.ModelVersion != v.ModelVersion || c.Versions.TeamStrengthVersion != v.TeamStrengthVersion ||
            c.Versions.GateVersion != v.GateVersion || c.Versions.CalibrationVersion != v.CalibrationVersion);
        Check($"rows not stamped with {v.Describe()}", "0", badVersion.ToString(), badVersion == 0);

        // ---- 8. evidence cutoff is strictly pre-match
        var lateEvidence = contracts.Count(c => c.EvidenceCutoff.HasValue && c.EvidenceCutoff.Value >= c.MatchDate);
        Check("predictions built on evidence dated at or after the match", "0",
            lateEvidence.ToString(), lateEvidence == 0);

        var backwards = contracts.Count(c => c.EvidenceCutoff.HasValue &&
            c.EvidenceCutoff.Value > DateOnly.FromDateTime(c.PredictionTimestamp));
        Check("predictions timestamped before their own evidence", "0", backwards.ToString(), backwards == 0);

        // ---- 10. identity and immutability
        var distinctIds = contracts.Select(c => c.PredictionId).Distinct().Count();
        Check("prediction ids are unique", contracts.Count.ToString(), distinctIds.ToString(),
            distinctIds == contracts.Count);

        Check("rows stored", contracts.Count.ToString(), store.Count.ToString(), store.Count == contracts.Count);
        Check("published predictions altered after publication", "0",
            store.TamperedRows().ToString(), store.TamperedRows() == 0);

        // ---- 4. confidence is not the probability
        var ranges = new List<(ConfidenceClass cls, double lo, double hi)>();
        foreach (var g in contracts.Where(c => c.PredictionEligible).GroupBy(c => c.ConfidenceClass))
        {
            var top = g.Select(c => Math.Max(c.HomeProbability!.Value,
                Math.Max(c.DrawProbability!.Value, c.AwayProbability!.Value))).ToList();
            ranges.Add((g.Key, top.Min(), top.Max()));
        }
        var disjoint = 0;
        for (var i = 0; i < ranges.Count; i++)
            for (var j = i + 1; j < ranges.Count; j++)
                if (ranges[i].hi < ranges[j].lo || ranges[j].hi < ranges[i].lo) disjoint++;
        var spans = string.Join(" ", ranges.OrderBy(r => r.cls).Select(r =>
            $"{r.cls.ToString().ToUpperInvariant()}:[{r.lo.ToString("0.00", CultureInfo.InvariantCulture)}-{r.hi.ToString("0.00", CultureInfo.InvariantCulture)}]"));
        Check("confidence classes occupying disjoint probability bands", "0", $"{disjoint} pairs  {spans}", disjoint == 0);

        var noneButPublished = contracts.Count(c => c.PredictionEligible && c.ConfidenceClass == ConfidenceClass.None);
        Check("published predictions declaring no evidence (ConfidenceClass NONE)", "0",
            noneButPublished.ToString(), noneButPublished == 0);

        // ---- 11/12. settlement did not change anything, and the log reproduces the metric
        Check("settled log loss recomputed from the store",
            contractLogLoss.ToString("0.00000000", CultureInfo.InvariantCulture),
            settledLogLoss.ToString("0.00000000", CultureInfo.InvariantCulture),
            Math.Abs(contractLogLoss - settledLogLoss) < 1e-12);

        // ---- 10. the store actually refuses a rewrite: probed live, not assumed
        Check("live probe: rewriting a published prediction", "REJECTED",
            immutabilityProbe.ToString().ToUpperInvariant(), immutabilityProbe == PublishOutcome.RejectedImmutable);
        Check("live probe: republishing an identical prediction", "ALREADY PUBLISHED (idempotent)",
            idempotentProbe.ToString().ToUpperInvariant(), idempotentProbe == PublishOutcome.AlreadyPublished);

        // ---- 2. the backend hands out decimals, not percentages
        // A probability that had been rounded for display would sit exactly on a 2-decimal value.
        // With genuine model output that essentially never happens; a large share would mean
        // something formatted the number before it reached the contract.
        var published = contracts.Count(c => c.PredictionEligible);
        var preRounded = 0;
        foreach (var c in contracts.Where(c => c.PredictionEligible))
            foreach (var p in new[] { c.HomeProbability!.Value, c.DrawProbability!.Value, c.AwayProbability!.Value })
                if (p is > 0.0 and < 1.0 && Math.Abs(p * 100 - Math.Round(p * 100)) < 1e-12) preRounded++;
        var share = published == 0 ? 0.0 : (double)preRounded / (published * 3);
        Check("published probabilities sitting exactly on a 2-decimal value (a sign of pre-rounding)",
            "< 1% of values", $"{preRounded} of {published * 3} ({share * 100:0.000}%)", share < 0.01);

        return res;
    }
}
