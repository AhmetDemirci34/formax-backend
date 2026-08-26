using System.Globalization;

namespace Formax.Prediction.Models;

/// <summary>
/// Why a match did not get a prediction. Machine codes, not prose: the backend states operational
/// status, it never explains what a probability means (that is not this layer's job, and per the
/// task it is not the backend's job at all).
/// </summary>
public enum GateCode
{
    /// <summary>One of the two teams is not an identity the model is willing to stand behind.</summary>
    IdentityUnconfirmed,
    /// <summary>A pre-match state the model requires does not exist for this match.</summary>
    SnapshotMissing,
    /// <summary>A side has no prior match at all: its rating IS the prior and carries nothing about that team.</summary>
    NoTeamHistory,
    /// <summary>A side has some history, but less than the configured minimum.</summary>
    InsufficientHistory,
    /// <summary>The competition has not been observed enough times for its goal baseline to be learned rather than seeded.</summary>
    CompetitionNotCovered,
    /// <summary>An expected-goals value sits on its clamp: the model state is saturated, not measured.</summary>
    ModelStateAtGuardRail,
    /// <summary>A rating, lambda or probability is not a finite, usable number.</summary>
    ModelStateInvalid,
    /// <summary>The three probabilities do not form a distribution.</summary>
    ProbabilityNotNormalised,
    /// <summary>Evidence dated at or after the match reached the prediction. Nothing may be published.</summary>
    EvidenceNotStrictlyPreMatch
}

/// <summary>
/// How much the underlying EVIDENCE is worth. This is not the probability and must never be derived
/// from it: a model can say 65% with LOW confidence (thin history on one side) or 40% with HIGH
/// confidence (both sides richly known, genuinely close match). Mixing the two is the single most
/// common way a probability output becomes dishonest.
/// </summary>
public enum ConfidenceClass { None, Low, MediumLow, Medium, High }

/// <summary>
/// The published prediction contract.
///
/// When <see cref="PredictionEligible"/> is false the three probabilities are NULL. They are not
/// zero, not a fallback, and not the prior: a match that did not pass the gate has no published
/// probability at all.
/// </summary>
public sealed class PredictionDto
{
    public required string MatchId { get; init; }
    public required DateOnly MatchDate { get; init; }

    /// <summary>Null unless <see cref="PredictionEligible"/>.</summary>
    public double? HomeProbability { get; init; }
    public double? DrawProbability { get; init; }
    public double? AwayProbability { get; init; }

    public required string ModelVersion { get; init; }
    public required string TeamStrengthVersion { get; init; }

    /// <summary>When this prediction was produced.</summary>
    public required DateTime PredictionTimestamp { get; init; }
    /// <summary>Date of the most recent match that fed either side's state. Always strictly before the match date.</summary>
    public required DateOnly? EvidenceCutoff { get; init; }

    public required ConfidenceClass ConfidenceClass { get; init; }
    public required bool PredictionEligible { get; init; }
    /// <summary>"OK" when eligible; otherwise the gate codes that fired, pipe separated.</summary>
    public required string GateReason { get; init; }

    // ---- audit only. Not part of the published contract; kept so the output layer can be proven
    // ---- not to have altered the model (see the regression check).
    public required double ModelHomeProbability { get; init; }
    public required double ModelDrawProbability { get; init; }
    public required double ModelAwayProbability { get; init; }
    public required double LambdaHome { get; init; }
    public required double LambdaAway { get; init; }
    public required string CompetitionType { get; init; }
    public required string Competition { get; init; }
    public required int HomePriorMatches { get; init; }
    public required int AwayPriorMatches { get; init; }
    public required string HomeColdStartClass { get; init; }
    public required string AwayColdStartClass { get; init; }
    public required int CompetitionMatchesObserved { get; init; }

    public double PublishedSum =>
        (HomeProbability ?? 0) + (DrawProbability ?? 0) + (AwayProbability ?? 0);

    private static string N(double v) => v.ToString("0.00000000", CultureInfo.InvariantCulture);
    private static string N(double? v) => v.HasValue ? N(v.Value) : string.Empty;
    private static string Q(string s) => s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    public static string CsvHeader =>
        "MatchId,MatchDate,PredictionEligible,GateReason,ConfidenceClass," +
        "HomeProbability,DrawProbability,AwayProbability,PublishedSum," +
        "ModelVersion,TeamStrengthVersion,PredictionTimestamp,EvidenceCutoff," +
        "ModelHomeProbability,ModelDrawProbability,ModelAwayProbability," +
        "LambdaHome,LambdaAway,Competition,CompetitionType,CompetitionMatchesObserved," +
        "HomePriorMatches,AwayPriorMatches,HomeColdStartClass,AwayColdStartClass";

    public string ToCsv() => string.Join(',',
        Q(MatchId), MatchDate.ToString("yyyy-MM-dd"),
        PredictionEligible ? "True" : "False", Q(GateReason), ConfidenceClass.ToString().ToUpperInvariant(),
        N(HomeProbability), N(DrawProbability), N(AwayProbability),
        PredictionEligible ? N(PublishedSum) : string.Empty,
        Q(ModelVersion), Q(TeamStrengthVersion),
        PredictionTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        EvidenceCutoff?.ToString("yyyy-MM-dd") ?? string.Empty,
        N(ModelHomeProbability), N(ModelDrawProbability), N(ModelAwayProbability),
        N(LambdaHome), N(LambdaAway), Q(Competition), Q(CompetitionType),
        CompetitionMatchesObserved.ToString(CultureInfo.InvariantCulture),
        HomePriorMatches.ToString(CultureInfo.InvariantCulture),
        AwayPriorMatches.ToString(CultureInfo.InvariantCulture),
        Q(HomeColdStartClass), Q(AwayColdStartClass));
}
