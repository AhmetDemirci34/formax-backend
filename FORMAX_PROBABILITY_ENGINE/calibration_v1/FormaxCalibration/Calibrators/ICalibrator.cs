using Formax.Calibration.Models;
using Formax.DixonColes.Models;

namespace Formax.Calibration.Calibrators;

/// <summary>
/// A map from a raw probability triple to a calibrated probability triple.
///
/// A calibrator sees ONLY (raw probability, outcome) pairs. It has no access to the rating, the
/// lambdas, the teams or the competition, so it cannot smuggle a model improvement in under the
/// name of calibration - which is exactly what this phase is not allowed to do.
///
/// Every implementation must guarantee the simplex: 0 &lt;= p &lt;= 1 for each class and a sum of
/// 1 to within 1e-12. That is enforced structurally (softmax, or clip-then-renormalise), not by
/// checking afterwards.
/// </summary>
public interface ICalibrator
{
    string Name { get; }
    /// <summary>Free parameters actually fitted. Reported so a 12-parameter fit is never mistaken for a 1-parameter one.</summary>
    int ParameterCount { get; }
    bool IsFitted { get; }

    /// <summary>Learns from past predictions only. The caller is responsible for never passing the future.</summary>
    void Fit(IReadOnlyList<CalibrationSample> history);

    ProbTriple Apply(ProbTriple raw);

    /// <summary>Human-readable parameter dump for the report.</summary>
    string DescribeParameters();

    /// <summary>JSON fragment (no outer braces) for calibration_model.json.</summary>
    string ParametersJson();

    ICalibrator FreshCopy();
}

/// <summary>The null calibrator: hands the raw probabilities straight back. The thing every method must beat.</summary>
public sealed class NoCalibration : ICalibrator
{
    public string Name => "NO_CALIBRATION";
    public int ParameterCount => 0;
    public bool IsFitted => true;
    public void Fit(IReadOnlyList<CalibrationSample> history) { }
    public ProbTriple Apply(ProbTriple raw) => raw;
    public string DescribeParameters() => "(none - raw model output)";
    public string ParametersJson() => "\"parameters\": null";
    public ICalibrator FreshCopy() => new NoCalibration();
}
