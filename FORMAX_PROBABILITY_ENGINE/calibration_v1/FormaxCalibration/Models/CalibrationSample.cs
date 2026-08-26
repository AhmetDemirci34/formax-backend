using Formax.DixonColes.Models;
using Formax.ModelValidation.Config;

namespace Formax.Calibration.Models;

/// <summary>
/// One raw prediction plus what actually happened. This is the ONLY thing a calibrator ever sees:
/// no team, no rating, no feature. Calibration learns a map from probability to probability, so
/// giving it anything else would make it a model, which this phase is not allowed to build.
/// </summary>
public readonly struct CalibrationSample
{
    public CalibrationSample(DateOnly date, ProbTriple raw, Outcome actual, Segment segment)
    { Date = date; Raw = raw; Actual = actual; Segment = segment; }

    public DateOnly Date { get; }
    public ProbTriple Raw { get; }
    public Outcome Actual { get; }
    public Segment Segment { get; }

    /// <summary>1 if class k happened, else 0.</summary>
    public double Y(int k) => (int)Actual == k ? 1.0 : 0.0;
}
