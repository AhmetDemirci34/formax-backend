using System.Globalization;
using Formax.DixonColes.Models;

namespace Formax.Contract.Models;

/// <summary>
/// What actually happened, attached to a prediction after the match.
///
/// A SEPARATE object on purpose. Settlement is not an update to the prediction - the prediction is
/// a statement made at a point in time and stays exactly as it was made. Putting the result in its
/// own record means there is no field on the prediction that settlement could even write to.
/// </summary>
public sealed record Settlement
{
    public required int ActualHomeGoals { get; init; }
    public required int ActualAwayGoals { get; init; }
    public required Outcome ActualResult { get; init; }
    public required DateTime SettlementTimestamp { get; init; }

    public static Outcome ResultOf(int home, int away)
        => home > away ? Outcome.HomeWin : home == away ? Outcome.Draw : Outcome.AwayWin;
}

/// <summary>One row of the append-only prediction log: the prediction, and later its settlement.</summary>
public sealed class PredictionRecord
{
    public required PredictionContract Prediction { get; init; }
    /// <summary>Recorded when the row was published, and re-checked afterwards.</summary>
    public required string PublishedContentHash { get; init; }

    /// <summary>
    /// Publication order, assigned by the store. This - not the timestamp - is what makes "which
    /// prediction is current" unambiguous: two predictions of the same match can legitimately share
    /// a timestamp (in replay both carry the match day), and then only the order they were appended
    /// in says which one supersedes the other. An append-only log already knows that order; the
    /// sequence just writes it down.
    /// </summary>
    public required long Sequence { get; init; }

    public Settlement? Settlement { get; private set; }

    /// <summary>Attaches a result. Refuses to settle twice; never touches the prediction.</summary>
    public void Attach(Settlement settlement)
    {
        if (Settlement is not null)
            throw new InvalidOperationException(
                $"prediction {Prediction.PredictionId} is already settled - a result may not be replaced");
        Settlement = settlement;
    }

    /// <summary>True when the prediction is still byte-for-byte what was published.</summary>
    public bool IsIntact => Prediction.ContentHash == PublishedContentHash;

    /// <summary>Log loss of this prediction, once settled. Null while unsettled or while unpublished.</summary>
    public double? LogLoss
    {
        get
        {
            if (Settlement is null || !Prediction.PredictionEligible) return null;
            var p = Settlement.ActualResult switch
            {
                Outcome.HomeWin => Prediction.HomeProbability!.Value,
                Outcome.Draw => Prediction.DrawProbability!.Value,
                _ => Prediction.AwayProbability!.Value
            };
            return -Math.Log(Math.Max(p, 1e-15));
        }
    }

    private static string N(double? v) => v.HasValue
        ? v.Value.ToString("0.00000000", CultureInfo.InvariantCulture) : string.Empty;
    private static string Q(string s) => s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    public static string CsvHeader =>
        "Sequence,PredictionId,MatchId,PredictionTimestamp,EvidenceCutoff," +
        "ModelVersion,TeamStrengthVersion,GateVersion,CalibrationVersion," +
        "HomeProbability,DrawProbability,AwayProbability,ConfidenceClass,GateStatus," +
        "ActualHomeGoals,ActualAwayGoals,ActualResult,SettlementTimestamp,LogLoss," +
        "PublishedContentHash,CurrentContentHash,Intact";

    public string ToCsv() => string.Join(',',
        Sequence.ToString(CultureInfo.InvariantCulture), Prediction.PredictionId, Q(Prediction.MatchId),
        Prediction.PredictionTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        Prediction.EvidenceCutoff?.ToString("yyyy-MM-dd") ?? string.Empty,
        Q(Prediction.Versions.ModelVersion), Q(Prediction.Versions.TeamStrengthVersion),
        Q(Prediction.Versions.GateVersion), Q(Prediction.Versions.CalibrationVersion),
        N(Prediction.HomeProbability), N(Prediction.DrawProbability), N(Prediction.AwayProbability),
        Prediction.ConfidenceClass.ToString().ToUpperInvariant(),
        Prediction.GateStatus.ToString().ToUpperInvariant(),
        Settlement?.ActualHomeGoals.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        Settlement?.ActualAwayGoals.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        Settlement?.ActualResult.ToString() ?? string.Empty,
        Settlement?.SettlementTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) ?? string.Empty,
        N(LogLoss), PublishedContentHash, Prediction.ContentHash,
        IsIntact ? "True" : "False");
}
