using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Formax.Contract.Models;

/// <summary>The gate's verdict, as a status rather than a sentence.</summary>
public enum GateStatus { Accepted, Rejected }

/// <summary>How much the EVIDENCE behind a prediction is worth. Never derived from the probability.</summary>
public enum ConfidenceClass { None, Low, MediumLow, Medium, High }

/// <summary>
/// The versions that produced a prediction. All four travel with every row, so a number found in a
/// log six months from now still says which model, which rating, which gate and which calibration
/// (or the absence of one) made it.
/// </summary>
public sealed record ContractVersions(
    string ModelVersion,
    string TeamStrengthVersion,
    string GateVersion,
    string CalibrationVersion)
{
    public static readonly ContractVersions Current = new(
        ModelVersion: "INDEPENDENT_POISSON_V2",
        TeamStrengthVersion: "TEAM_STRENGTH_V2",
        GateVersion: "GATE_V1",
        CalibrationVersion: "NONE");

    public string Describe() =>
        $"model={ModelVersion} strength={TeamStrengthVersion} gate={GateVersion} calibration={CalibrationVersion}";
}

/// <summary>
/// THE canonical prediction the backend returns. One type, one shape, no variants.
///
/// IMMUTABLE BY CONSTRUCTION. Every member is init-only, so nothing downstream - settlement,
/// reporting, a narrative layer - can reach in and change a published number. That is not a
/// convention this code asks you to respect; it is the only thing the language will allow.
///
/// PROBABILITY MEANING (the definition the rest of FORMAX must quote):
///
///   HomeProbability - the model's estimated probability that the HOME side wins the match in
///                     normal result terms: the score line that stands when the match is decided
///                     the way the competition records it (extra time included where the dataset
///                     records it, penalty shoot-outs excluded).
///   DrawProbability - the same, for the match ending level.
///   AwayProbability - the same, for the AWAY side winning.
///
/// They are DECIMALS in [0,1] summing to 1. The backend never formats them as strings, never
/// rounds them and never appends a percent sign: turning 0.6452 into "65%" is a presentation
/// decision and belongs to whoever is presenting.
///
/// When <see cref="PredictionEligible"/> is false the three probabilities are NULL. Not zero, not
/// the prior, not a fallback - absent.
/// </summary>
public sealed record PredictionContract
{
    /// <summary>Identity of this exact prediction. Two predictions of the same match at different evidence are different ids.</summary>
    public required string PredictionId { get; init; }
    public required string MatchId { get; init; }

    public required DateTime PredictionTimestamp { get; init; }
    /// <summary>The newest match that could have informed this prediction. Always strictly before the match date.</summary>
    public required DateOnly? EvidenceCutoff { get; init; }
    public required DateOnly MatchDate { get; init; }

    public required ContractVersions Versions { get; init; }

    public required double? HomeProbability { get; init; }
    public required double? DrawProbability { get; init; }
    public required double? AwayProbability { get; init; }

    public required bool PredictionEligible { get; init; }
    public required ConfidenceClass ConfidenceClass { get; init; }
    public required GateStatus GateStatus { get; init; }
    /// <summary>"OK" when accepted; otherwise the gate codes that fired, pipe separated.</summary>
    public required string GateReason { get; init; }

    public double ProbabilitySum =>
        (HomeProbability ?? 0) + (DrawProbability ?? 0) + (AwayProbability ?? 0);

    /// <summary>
    /// A fingerprint of everything a consumer is allowed to rely on. Recorded at publish time and
    /// re-checked afterwards, so "settlement did not touch the prediction" is a measurement rather
    /// than a promise.
    /// </summary>
    public string ContentHash
    {
        get
        {
            static string N(double? v) => v.HasValue
                ? v.Value.ToString("R", CultureInfo.InvariantCulture) : "null";
            var canonical = string.Join('|',
                PredictionId, MatchId,
                PredictionTimestamp.ToString("O", CultureInfo.InvariantCulture),
                EvidenceCutoff?.ToString("yyyy-MM-dd") ?? "null",
                MatchDate.ToString("yyyy-MM-dd"),
                Versions.ModelVersion, Versions.TeamStrengthVersion,
                Versions.GateVersion, Versions.CalibrationVersion,
                N(HomeProbability), N(DrawProbability), N(AwayProbability),
                PredictionEligible ? "1" : "0",
                ConfidenceClass.ToString(), GateStatus.ToString(), GateReason);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..32];
        }
    }

    private static string N(double? v) => v.HasValue
        ? v.Value.ToString("0.00000000", CultureInfo.InvariantCulture) : string.Empty;
    private static string Q(string s) => s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    public static string CsvHeader =>
        "PredictionId,MatchId,MatchDate,PredictionTimestamp,EvidenceCutoff," +
        "ModelVersion,TeamStrengthVersion,GateVersion,CalibrationVersion," +
        "HomeProbability,DrawProbability,AwayProbability,ProbabilitySum," +
        "PredictionEligible,ConfidenceClass,GateStatus,GateReason,ContentHash";

    public string ToCsv() => string.Join(',',
        PredictionId, Q(MatchId), MatchDate.ToString("yyyy-MM-dd"),
        PredictionTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        EvidenceCutoff?.ToString("yyyy-MM-dd") ?? string.Empty,
        Q(Versions.ModelVersion), Q(Versions.TeamStrengthVersion),
        Q(Versions.GateVersion), Q(Versions.CalibrationVersion),
        N(HomeProbability), N(DrawProbability), N(AwayProbability),
        PredictionEligible ? N(ProbabilitySum) : string.Empty,
        PredictionEligible ? "True" : "False",
        ConfidenceClass.ToString().ToUpperInvariant(),
        GateStatus.ToString().ToUpperInvariant(),
        Q(GateReason), ContentHash);
}
