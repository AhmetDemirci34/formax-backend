using Formax.Calibration.Calibrators;
using Formax.Calibration.Models;
using Formax.DixonColes.Models;
using Formax.ModelValidation.Config;

namespace Formax.Calibration.Services;

/// <summary>How a calibrator is allowed to learn over time.</summary>
public enum FitRegime
{
    /// <summary>Fitted once on TRAIN, then frozen for VALIDATION and TEST. Nothing is ever refitted.</summary>
    StaticTrain,
    /// <summary>Refitted at the start of every month on every match played strictly earlier.</summary>
    Expanding
}

public sealed class RefitPoint
{
    public required DateOnly EffectiveFrom { get; init; }
    public required int HistorySize { get; init; }
    public required DateOnly? HistoryLatestDate { get; init; }
    public required bool Active { get; init; }
    public required string Parameters { get; init; }
}

public sealed class CalibrationRun
{
    public required string Method { get; init; }
    public required FitRegime Regime { get; init; }
    public required string Key { get; init; }
    public required ProbTriple[] Calibrated { get; init; }
    public required ICalibrator FinalModel { get; init; }
    public List<RefitPoint> RefitTrace { get; } = new();
    /// <summary>Latest match date any fit of this run was allowed to see. Must stay below the test boundary for anything used in selection.</summary>
    public DateOnly? LatestTrainingDate { get; set; }
}

/// <summary>
/// Applies a calibrator across the whole timeline under one of the two fit regimes.
///
/// TEMPORAL CONTRACT - both regimes obey it, and it is checked rather than asserted:
/// a calibrated probability for a match on date d is produced by a calibrator fitted only on
/// matches played STRICTLY BEFORE d. In the static regime that is guaranteed by fitting on TRAIN
/// alone; in the expanding regime by cutting the history at the first day of the month the match
/// falls in, which is itself at or before d.
///
/// The expanding regime has a warm-up: until MinHistory matches exist there is nothing to fit, so
/// the raw probability is passed through unchanged and the refit trace records that the calibrator
/// was inactive. Pretending to calibrate on 30 matches would be worse than not calibrating.
/// </summary>
public static class CalibrationRunner
{
    /// <summary>Below this many past matches the expanding regime does not calibrate at all.</summary>
    public const int MinHistory = 500;

    public static string KeyOf(string method, FitRegime regime)
        => regime == FitRegime.StaticTrain ? $"{method}__STATIC_TRAIN" : $"{method}__EXPANDING";

    public static CalibrationRun Run(
        ICalibrator prototype, FitRegime regime,
        IReadOnlyList<CalibrationSample> samplesInDateOrder, SplitConfig split)
    {
        return regime == FitRegime.StaticTrain
            ? RunStatic(prototype, samplesInDateOrder, split)
            : RunExpanding(prototype, samplesInDateOrder);
    }

    private static CalibrationRun RunStatic(
        ICalibrator prototype, IReadOnlyList<CalibrationSample> samples, SplitConfig split)
    {
        var train = samples.Where(s => s.Segment == Segment.Train).ToList();
        var model = prototype.FreshCopy();
        model.Fit(train);

        var calibrated = new ProbTriple[samples.Count];
        for (var i = 0; i < samples.Count; i++) calibrated[i] = model.Apply(samples[i].Raw);

        var run = new CalibrationRun
        {
            Method = prototype.Name,
            Regime = FitRegime.StaticTrain,
            Key = KeyOf(prototype.Name, FitRegime.StaticTrain),
            Calibrated = calibrated,
            FinalModel = model,
            LatestTrainingDate = train.Count == 0 ? null : train.Max(s => s.Date)
        };
        run.RefitTrace.Add(new RefitPoint
        {
            EffectiveFrom = split.ValidationStartDate,
            HistorySize = train.Count,
            HistoryLatestDate = run.LatestTrainingDate,
            Active = model.IsFitted,
            Parameters = model.DescribeParameters()
        });
        return run;
    }

    private static CalibrationRun RunExpanding(ICalibrator prototype, IReadOnlyList<CalibrationSample> samples)
    {
        // month boundaries, in order
        var months = samples.Select(s => new DateOnly(s.Date.Year, s.Date.Month, 1)).Distinct().OrderBy(d => d).ToList();

        // cut index per month: the number of samples played strictly before that month
        var cut = new int[months.Count];
        for (var m = 0; m < months.Count; m++)
        {
            var lo = 0; var hi = samples.Count;
            while (lo < hi) { var mid = (lo + hi) / 2; if (samples[mid].Date < months[m]) lo = mid + 1; else hi = mid; }
            cut[m] = lo;
        }

        // fit every month independently - each only ever reads samples[0 .. cut[m])
        var models = new ICalibrator[months.Count];
        Parallel.For(0, months.Count, m =>
        {
            if (cut[m] < MinHistory) { models[m] = new NoCalibration(); return; }
            var history = new List<CalibrationSample>(cut[m]);
            for (var i = 0; i < cut[m]; i++) history.Add(samples[i]);
            var model = prototype.FreshCopy();
            model.Fit(history);
            models[m] = model;
        });

        var calibrated = new ProbTriple[samples.Count];
        var monthOf = new Dictionary<DateOnly, int>();
        for (var m = 0; m < months.Count; m++) monthOf[months[m]] = m;
        for (var i = 0; i < samples.Count; i++)
        {
            var m = monthOf[new DateOnly(samples[i].Date.Year, samples[i].Date.Month, 1)];
            calibrated[i] = models[m].Apply(samples[i].Raw);
        }

        var run = new CalibrationRun
        {
            Method = prototype.Name,
            Regime = FitRegime.Expanding,
            Key = KeyOf(prototype.Name, FitRegime.Expanding),
            Calibrated = calibrated,
            FinalModel = models[^1],
            LatestTrainingDate = cut[^1] > 0 ? samples[cut[^1] - 1].Date : null
        };
        for (var m = 0; m < months.Count; m++)
            run.RefitTrace.Add(new RefitPoint
            {
                EffectiveFrom = months[m],
                HistorySize = cut[m],
                HistoryLatestDate = cut[m] > 0 ? samples[cut[m] - 1].Date : null,
                Active = models[m].IsFitted && models[m] is not NoCalibration,
                Parameters = models[m].DescribeParameters()
            });
        return run;
    }

    /// <summary>
    /// The temporal audit: for every refit, the newest match it was allowed to see must be strictly
    /// earlier than the first match it is used on. Returns the number of violations, which must be 0.
    /// </summary>
    public static int TemporalViolations(CalibrationRun run, IReadOnlyList<CalibrationSample> samples)
    {
        var violations = 0;
        foreach (var point in run.RefitTrace)
        {
            if (!point.Active || point.HistoryLatestDate is null) continue;
            var firstUsed = samples.Where(s => s.Date >= point.EffectiveFrom)
                                   .Select(s => (DateOnly?)s.Date).FirstOrDefault();
            if (firstUsed is null) continue;
            if (point.HistoryLatestDate.Value >= firstUsed.Value) violations++;
        }
        return violations;
    }
}
