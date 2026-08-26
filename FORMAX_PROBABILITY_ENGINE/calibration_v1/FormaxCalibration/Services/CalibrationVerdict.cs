namespace Formax.Calibration.Services;

/// <summary>
/// The decision rule of this phase, written down once and applied mechanically.
///
/// A calibration method is only called an improvement when ALL of the following hold:
///   1. it beat the raw model on VALIDATION log loss - the criterion it was selected on,
///   2. it beats the raw model on TEST log loss,
///   3. the paired bootstrap interval on TEST log loss excludes zero,
///   4. it does not lose on TEST Brier or RPS,
///   5. it is not wildly unbalanced across competition types.
///
/// Lowering calibration error alone is explicitly NOT enough: a transform can look better on a
/// reliability curve while being worse at the thing the curve is a proxy for.
/// </summary>
public static class CalibrationVerdict
{
    /// <summary>A competition type is "broken" by calibration when it loses log loss by more than this.</summary>
    public const double CompetitionTolerance = 0.0050;
    /// <summary>Below this, a log loss gain is real but too small to act on.</summary>
    public const double MeaningfulLogLoss = 0.0010;
    /// <summary>Slices below this size cannot decide anything.</summary>
    public const int MinSliceForDecision = 100;

    public static (List<string> lines, bool proven) Decide(
        Score validationSelected, Score validationRaw,
        Score testSelected, Score testRaw,
        CalibrationDelta testBootstrap,
        IReadOnlyList<CalibrationDelta> byCompetition)
    {
        var lines = new List<string>();
        var name = validationSelected.Method == "NO_CALIBRATION"
            ? "NO_CALIBRATION"
            : $"{validationSelected.Method} ({validationSelected.Regime})";

        var valGain = validationSelected.LogLoss - validationRaw.LogLoss;
        var testGain = testSelected.LogLoss - testRaw.LogLoss;
        var brierGain = testSelected.Brier - testRaw.Brier;
        var rpsGain = testSelected.Rps - testRaw.Rps;
        var eceGain = testSelected.CalibrationError - testRaw.CalibrationError;

        var beatsOnValidation = valGain < 0;
        var beatsOnTest = testGain < 0;
        var significant = testBootstrap.LogLossSignificant && testBootstrap.DeltaLogLoss < 0;
        var noMetricLost = brierGain <= 0 && rpsGain <= 0;
        var meaningful = Math.Abs(testGain) >= MeaningfulLogLoss;

        var broken = byCompetition
            .Where(c => c.N >= MinSliceForDecision && c.DeltaLogLoss > CompetitionTolerance)
            .Select(c => $"{c.Group} (N={c.N}, {c.DeltaLogLoss:+0.0000})")
            .ToList();

        lines.Add($"CALIBRATION DECISION - selected method: {name}");
        lines.Add($"  VALIDATION log loss {validationSelected.LogLoss:0.000000} vs raw {validationRaw.LogLoss:0.000000}   " +
                  $"delta {valGain:+0.000000;-0.000000;0}");
        lines.Add($"  TEST       log loss {testSelected.LogLoss:0.000000} vs raw {testRaw.LogLoss:0.000000}   " +
                  $"delta {testGain:+0.000000;-0.000000;0}   95% CI [{testBootstrap.LogLossCiLow:+0.00000;-0.00000;0}, {testBootstrap.LogLossCiHigh:+0.00000;-0.00000;0}]");
        lines.Add($"  TEST       Brier delta {brierGain:+0.000000;-0.000000;0}   RPS delta {rpsGain:+0.000000;-0.000000;0}   " +
                  $"calibration error {testRaw.CalibrationError:0.00000} -> {testSelected.CalibrationError:0.00000} ({eceGain:+0.00000;-0.00000;0})");
        lines.Add($"  size: {(meaningful ? "operationally meaningful" : $"below the {MeaningfulLogLoss:0.0000} threshold declared in advance")} " +
                  $"({(Math.Exp(Math.Abs(testGain)) - 1.0) * 100:0.000}% change in the probability given to what actually happened)");
        lines.Add($"  competition types damaged beyond {CompetitionTolerance:0.0000} log loss: " +
                  (broken.Count == 0 ? "none" : string.Join(", ", broken)));

        var proven = beatsOnValidation && beatsOnTest && significant && noMetricLost && meaningful && broken.Count == 0;

        if (!beatsOnValidation)
            lines.Add("  VERDICT: CALIBRATION_IMPROVEMENT_NOT_PROVEN - no method beat the raw model on validation.");
        else if (!beatsOnTest)
            lines.Add("  VERDICT: CALIBRATION_IMPROVEMENT_NOT_PROVEN - the validation gain did not survive on test.");
        else if (!significant)
            lines.Add("  VERDICT: CALIBRATION_IMPROVEMENT_NOT_PROVEN - the test gain is inside the bootstrap interval.");
        else if (!noMetricLost)
            lines.Add("  VERDICT: CALIBRATION_IMPROVEMENT_NOT_PROVEN - log loss improved but Brier or RPS got worse.");
        else if (!meaningful)
            lines.Add("  VERDICT: CALIBRATION_IMPROVEMENT_NOT_PROVEN - the gain is real but below the operational threshold.");
        else if (broken.Count > 0)
            lines.Add("  VERDICT: CALIBRATION_IMPROVEMENT_PROVEN OVERALL, BUT UNBALANCED ACROSS COMPETITIONS.");
        else
            lines.Add("  VERDICT: CALIBRATION_IMPROVEMENT_PROVEN");

        return (lines, proven);
    }
}
