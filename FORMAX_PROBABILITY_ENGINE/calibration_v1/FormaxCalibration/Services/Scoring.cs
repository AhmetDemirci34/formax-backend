using System.Globalization;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;

namespace Formax.Calibration.Services;

/// <summary>Every number the decision rule of this phase is allowed to look at, for one slice.</summary>
public sealed class Score
{
    public required string Method { get; init; }
    public required string Regime { get; init; }
    public required string Segment { get; init; }
    public required string Scope { get; init; }
    public required string Group { get; init; }
    /// <summary>True when the calibrator was fitted on exactly these matches. Such a row is not evidence of anything.</summary>
    public required bool InSample { get; init; }

    public required int N { get; init; }
    public required double LogLoss { get; init; }
    public required double Brier { get; init; }
    public required double Rps { get; init; }
    public required double Accuracy { get; init; }
    public required double CalibrationError { get; init; }
    public required double MaxCalibrationError { get; init; }

    private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);
    private static string Q(string s) => s.Contains(',') ? "\"" + s + "\"" : s;

    public static string CsvHeader =>
        "Method,Regime,Segment,Scope,Group,InSample,N,LogLoss,Brier,RPS,Accuracy,CalibrationError,MaxCalibrationError";

    public string ToCsv() => string.Join(',', Q(Method), Q(Regime), Q(Segment), Q(Scope), Q(Group),
        InSample ? "True" : "False", N.ToString(CultureInfo.InvariantCulture),
        F(LogLoss), F(Brier), F(Rps), F(Accuracy), F(CalibrationError), F(MaxCalibrationError));

    public static Score Of(IReadOnlyList<(ProbTriple p, Outcome actual)> data,
        string method, string regime, string segment, string scope, string group, bool inSample)
    {
        var acc = new MetricAccumulator(method, scope, group);
        foreach (var (p, a) in data) acc.Add(p, a);
        var (ece, mce) = CalibrationMetrics.Error(CalibrationMetrics.AllClassPairs(data));
        return new Score
        {
            Method = method,
            Regime = regime,
            Segment = segment,
            Scope = scope,
            Group = group,
            InSample = inSample,
            N = acc.N,
            LogLoss = acc.LogLoss,
            Brier = acc.Brier,
            Rps = acc.Rps,
            Accuracy = acc.Accuracy,
            CalibrationError = ece,
            MaxCalibrationError = mce
        };
    }
}
