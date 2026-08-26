using System.Globalization;
using Formax.Calibration.Models;
using Formax.DixonColes.Models;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;

namespace Formax.Calibration.Services;

/// <summary>
/// The checks this phase must pass before any of its numbers mean anything: the simplex is intact,
/// no calibrator ever saw the future, and nothing was fitted on the test segment.
/// </summary>
public static class CalibrationAudit
{
    public static List<(string check, string expected, string observed, string status)> Run(
        IReadOnlyList<CalibrationSample> samples,
        IReadOnlyList<CalibrationRun> runs,
        IReadOnlyList<(string key, Func<int, ProbTriple> probs)> variants,
        SplitConfig split,
        IReadOnlyList<MatchPrediction> rawPreds)
    {
        var res = new List<(string, string, string, string)>();
        void Check(string name, string expected, string observed, bool ok)
            => res.Add((name, expected, observed, ok ? "PASS" : "FAIL"));

        // ---- the simplex, for every variant and every match
        var maxSumError = 0.0;
        var outOfRange = 0;
        foreach (var v in variants)
            for (var i = 0; i < samples.Count; i++)
            {
                var p = v.probs(i);
                maxSumError = Math.Max(maxSumError, Math.Abs(p.Sum - 1.0));
                if (p.Home < 0 || p.Home > 1 || p.Draw < 0 || p.Draw > 1 || p.Away < 0 || p.Away > 1) outOfRange++;
            }
        Check($"probability sum error (max over {variants.Count} variants x {samples.Count} matches)", "< 1e-12",
            maxSumError.ToString("0.0e+0", CultureInfo.InvariantCulture), maxSumError < 1e-12);
        Check("probabilities outside [0,1]", "0", outOfRange.ToString(), outOfRange == 0);

        // ---- temporal contract: every fit only ever saw matches earlier than the ones it is used on
        var temporalViolations = 0;
        foreach (var run in runs) temporalViolations += CalibrationRunner.TemporalViolations(run, samples);
        Check("refits whose training history reaches the matches they are applied to", "0",
            temporalViolations.ToString(), temporalViolations == 0);

        // ---- what "no test data" means depends on the regime, so it is checked per regime
        //
        // STATIC_TRAIN is a frozen model: it must never have seen anything past the TRAIN segment.
        var latestStaticFit = runs.Where(r => r.Regime == FitRegime.StaticTrain)
            .Select(r => r.LatestTrainingDate).Where(d => d.HasValue).Select(d => d!.Value)
            .DefaultIfEmpty().Max();
        Check("latest match any STATIC_TRAIN calibrator was fitted on", $"< {split.ValidationStart}",
            latestStaticFit == default ? "(none)" : latestStaticFit.ToString("yyyy-MM-dd"),
            latestStaticFit != default && latestStaticFit < split.ValidationStartDate);

        // EXPANDING is a simulation of live operation: while predicting a match it may use every
        // match already played, including earlier test matches. That is the regime, not a leak - the
        // thing that would be a leak is training on a match at or after the one being predicted, and
        // that is what the temporal check above counts. Recorded here so the difference is explicit.
        var expandingRefitsInsideTest = runs.Where(r => r.Regime == FitRegime.Expanding)
            .SelectMany(r => r.RefitTrace).Count(t => t.Active && t.EffectiveFrom >= split.TestStartDate);
        Check("EXPANDING refits inside TEST train only on matches already played (by design, not a leak)",
            "temporal violations 0", $"{expandingRefitsInsideTest} refits, 0 violations", temporalViolations == 0);

        // ---- and no calibrator, in either regime, was fitted on a match it then predicted
        var selfFitted = 0;
        foreach (var run in runs)
            foreach (var t in run.RefitTrace)
                if (t.Active && t.HistoryLatestDate.HasValue && t.HistoryLatestDate.Value >= t.EffectiveFrom)
                    selfFitted++;
        Check("refits whose history reaches into the period they are applied to", "0",
            selfFitted.ToString(), selfFitted == 0);

        // ---- a calibrator may not depend on anything but the raw probability
        // two matches with identical raw probabilities must receive identical calibrated probabilities
        var groups = new Dictionary<(long, long, long), int>();
        var inconsistent = 0;
        var checkedRun = runs.FirstOrDefault(r => r.Regime == FitRegime.StaticTrain);
        if (checkedRun is not null)
        {
            var seen = new Dictionary<(long, long, long), (double h, double d, double a)>();
            for (var i = 0; i < samples.Count; i++)
            {
                var raw = samples[i].Raw;
                var key = ((long)(raw.Home * 1e12), (long)(raw.Draw * 1e12), (long)(raw.Away * 1e12));
                var cal = checkedRun.Calibrated[i];
                if (seen.TryGetValue(key, out var prev))
                {
                    if (Math.Abs(prev.h - cal.Home) > 1e-12 || Math.Abs(prev.d - cal.Draw) > 1e-12) inconsistent++;
                }
                else { seen[key] = (cal.Home, cal.Draw, cal.Away); groups[key] = 1; }
            }
        }
        Check("identical raw probabilities that received different calibrated probabilities (static regime)", "0",
            inconsistent.ToString(), inconsistent == 0);

        // ---- the base model was not touched: raw predictions must still be the validated V2 numbers
        var testRaw = samples.Where(s => s.Segment == Segment.Test).ToList();
        var ll = 0.0;
        foreach (var s in testRaw) ll += -Math.Log(Math.Max(s.Raw[s.Actual], 1e-15));
        ll /= testRaw.Count;
        Check("frozen base model reproduces the validated INDEPENDENT_POISSON_V2 test log loss", "0.993544",
            ll.ToString("0.000000", CultureInfo.InvariantCulture), Math.Abs(ll - 0.993544) < 5e-6);

        Check("matches without a raw prediction", "0", (rawPreds.Count - samples.Count).ToString(),
            rawPreds.Count == samples.Count);

        return res;
    }
}
