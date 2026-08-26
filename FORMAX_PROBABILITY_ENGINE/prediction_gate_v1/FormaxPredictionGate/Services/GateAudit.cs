using System.Globalization;
using Formax.ModelValidation.Models;
using Formax.Prediction.Config;
using Formax.Prediction.Models;

namespace Formax.Prediction.Services;

/// <summary>
/// The checks the output contract must pass before any DTO it produces means anything.
/// </summary>
public static class GateAudit
{
    public static List<(string check, string expected, string observed, string status)> Run(
        IReadOnlyList<PredictionDto> dtos,
        IReadOnlyList<MatchPrediction> raws,
        GateConfig cfg,
        bool regressionOk,
        double settledLogLoss,
        IReadOnlyList<PredictionLogRecord> log)
    {
        var res = new List<(string, string, string, string)>();
        void Check(string name, string expected, string observed, bool ok)
            => res.Add((name, expected, observed, ok ? "PASS" : "FAIL"));

        // ---- 7. the output layer did not change the model
        Check("published metrics reproduce the validated V2 baseline on every segment", "MATCH",
            regressionOk ? "MATCH" : "DIFFERS", regressionOk);

        // ---- the DTO never alters a probability it publishes
        var altered = 0;
        var maxDelta = 0.0;
        foreach (var d in dtos.Where(d => d.PredictionEligible))
        {
            var dh = Math.Abs(d.HomeProbability!.Value - d.ModelHomeProbability);
            var dd = Math.Abs(d.DrawProbability!.Value - d.ModelDrawProbability);
            var da = Math.Abs(d.AwayProbability!.Value - d.ModelAwayProbability);
            maxDelta = Math.Max(maxDelta, Math.Max(dh, Math.Max(dd, da)));
            if (dh > 0 || dd > 0 || da > 0) altered++;
        }
        Check("published probabilities that differ from the model output", "0",
            $"{altered} (max delta {maxDelta.ToString("0.0e+0", CultureInfo.InvariantCulture)})", altered == 0);

        // ---- 1. the simplex, on everything published
        var maxSumError = 0.0;
        var outOfRange = 0;
        foreach (var d in dtos.Where(d => d.PredictionEligible))
        {
            maxSumError = Math.Max(maxSumError, Math.Abs(d.PublishedSum - 1.0));
            foreach (var p in new[] { d.HomeProbability!.Value, d.DrawProbability!.Value, d.AwayProbability!.Value })
                if (p < 0 || p > 1) outOfRange++;
        }
        Check("published probability sum error", $"<= {cfg.ProbabilitySumTolerance:0.0e+0}",
            maxSumError.ToString("0.0e+0", CultureInfo.InvariantCulture), maxSumError <= cfg.ProbabilitySumTolerance);
        Check("published probabilities outside [0,1]", "0", outOfRange.ToString(), outOfRange == 0);

        // ---- 2. a refused match publishes nothing at all
        var leakedNumbers = dtos.Count(d => !d.PredictionEligible &&
            (d.HomeProbability.HasValue || d.DrawProbability.HasValue || d.AwayProbability.HasValue));
        Check("refused matches that still carry a published probability", "0",
            leakedNumbers.ToString(), leakedNumbers == 0);

        var emptyReason = dtos.Count(d => !d.PredictionEligible && string.IsNullOrWhiteSpace(d.GateReason));
        Check("refused matches without a gate reason", "0", emptyReason.ToString(), emptyReason == 0);

        var eligibleWithReason = dtos.Count(d => d.PredictionEligible && d.GateReason != "OK");
        Check("eligible matches carrying a refusal reason", "0", eligibleWithReason.ToString(), eligibleWithReason == 0);

        // ---- 4. no artificial clipping: the highest published value equals the highest the model produced,
        // provided that match passed the gate
        var eligibleModelMax = dtos.Where(d => d.PredictionEligible)
            .Select(d => Math.Max(d.ModelHomeProbability, Math.Max(d.ModelDrawProbability, d.ModelAwayProbability)))
            .DefaultIfEmpty().Max();
        var publishedMax = dtos.Where(d => d.PredictionEligible)
            .Select(d => Math.Max(d.HomeProbability!.Value, Math.Max(d.DrawProbability!.Value, d.AwayProbability!.Value)))
            .DefaultIfEmpty().Max();
        Check("highest published probability equals the highest the model produced (no clipping)",
            eligibleModelMax.ToString("0.000000", CultureInfo.InvariantCulture),
            publishedMax.ToString("0.000000", CultureInfo.InvariantCulture),
            Math.Abs(eligibleModelMax - publishedMax) == 0.0);

        // ---- no leakage: evidence is strictly older than the match, on every published row
        var lateEvidence = dtos.Count(d => d.PredictionEligible &&
            d.EvidenceCutoff.HasValue && d.EvidenceCutoff.Value >= d.MatchDate);
        Check("published predictions built on evidence dated at or after the match", "0",
            lateEvidence.ToString(), lateEvidence == 0);

        // ---- the timestamp never precedes the evidence it was built from
        var backwards = dtos.Count(d => d.EvidenceCutoff.HasValue &&
            d.EvidenceCutoff.Value > DateOnly.FromDateTime(d.PredictionTimestamp));
        Check("predictions timestamped before their own evidence", "0", backwards.ToString(), backwards == 0);

        // ---- 3. confidence is not a restatement of the probability
        // If confidence were derived from the probability, each class would occupy its own band and
        // the bands would not overlap. So: measure the probability range of every class actually
        // present and require every pair of them to overlap.
        var ranges = new List<(ConfidenceClass cls, double lo, double hi)>();
        foreach (var g in dtos.Where(d => d.PredictionEligible).GroupBy(d => d.ConfidenceClass))
        {
            var top = g.Select(d => Math.Max(d.HomeProbability!.Value,
                Math.Max(d.DrawProbability!.Value, d.AwayProbability!.Value))).ToList();
            ranges.Add((g.Key, top.Min(), top.Max()));
        }
        ranges.Sort((x, y) => x.cls.CompareTo(y.cls));

        var disjointPairs = 0;
        for (var i = 0; i < ranges.Count; i++)
            for (var j = i + 1; j < ranges.Count; j++)
                if (ranges[i].hi < ranges[j].lo || ranges[j].hi < ranges[i].lo) disjointPairs++;

        var spans = string.Join(" ", ranges.Select(r =>
            $"{r.cls.ToString().ToUpperInvariant()}:[{r.lo.ToString("0.00", CultureInfo.InvariantCulture)}-{r.hi.ToString("0.00", CultureInfo.InvariantCulture)}]"));
        Check("confidence classes that occupy disjoint probability bands (they would, if confidence were the probability)",
            "0", $"{disjointPairs} of {ranges.Count * (ranges.Count - 1) / 2} pairs  {spans}", disjointPairs == 0);

        // ---- a published prediction may never declare that it has no evidence
        var publishedWithoutConfidence = dtos.Count(d => d.PredictionEligible && d.ConfidenceClass == ConfidenceClass.None);
        Check("published predictions carrying ConfidenceClass NONE", "0",
            publishedWithoutConfidence.ToString(), publishedWithoutConfidence == 0);

        // ---- 6. the log round-trips: every DTO produced exactly one log row, and settling reproduces the metric
        Check("log rows written per prediction", dtos.Count.ToString(), log.Count.ToString(), log.Count == dtos.Count);

        var publishedSettled = log.Count(r => r.LogLoss.HasValue);
        var publishedDtos = dtos.Count(d => d.PredictionEligible);
        Check("published predictions that could be settled against a real result",
            publishedDtos.ToString(), publishedSettled.ToString(), publishedSettled == publishedDtos);

        // the log recomputes the same number the metric layer did, from its own stored fields
        var fromDtos = 0.0; var n = 0;
        for (var i = 0; i < dtos.Count; i++)
        {
            if (!dtos[i].PredictionEligible) continue;
            var p = raws[i].Actual switch
            {
                DixonColes.Models.Outcome.HomeWin => dtos[i].HomeProbability!.Value,
                DixonColes.Models.Outcome.Draw => dtos[i].DrawProbability!.Value,
                _ => dtos[i].AwayProbability!.Value
            };
            fromDtos += -Math.Log(Math.Max(p, 1e-15));
            n++;
        }
        fromDtos = n == 0 ? double.NaN : fromDtos / n;
        Check("settled log reproduces the published log loss",
            fromDtos.ToString("0.00000000", CultureInfo.InvariantCulture),
            settledLogLoss.ToString("0.00000000", CultureInfo.InvariantCulture),
            Math.Abs(fromDtos - settledLogLoss) < 1e-12);

        return res;
    }
}
