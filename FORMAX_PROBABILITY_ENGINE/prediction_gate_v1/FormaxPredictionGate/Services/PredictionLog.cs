using System.Globalization;
using Formax.DixonColes.Models;
using Formax.Prediction.Models;

namespace Formax.Prediction.Services;

/// <summary>
/// One logged prediction, written at the moment of prediction with the outcome column empty.
///
/// The point of this record is that it can be SETTLED later: when the match finishes, the result is
/// joined onto the row it was predicted for, and the model can be scored against reality without
/// anyone re-running history. That is the difference between a system that measures itself and one
/// that only measured itself once, in a backtest, on a laptop.
///
/// Deliberately flat and self-describing: the model version and the evidence cutoff travel WITH the
/// row, so a score computed six months from now still knows which model produced it and what it
/// could see at the time.
/// </summary>
public sealed class PredictionLogRecord
{
    public required string MatchId { get; init; }
    public required DateTime PredictionTimestamp { get; init; }
    public required DateOnly? EvidenceCutoff { get; init; }
    public required string ModelVersion { get; init; }
    public required string TeamStrengthVersion { get; init; }
    public required double? HomeProbability { get; init; }
    public required double? DrawProbability { get; init; }
    public required double? AwayProbability { get; init; }
    public required ConfidenceClass ConfidenceClass { get; init; }
    /// <summary>ELIGIBLE, or the gate codes that refused it.</summary>
    public required string GateStatus { get; init; }

    // ---- filled in when the match finishes; empty at prediction time
    public Outcome? ActualOutcome { get; set; }
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    public DateOnly? SettledOn { get; set; }

    /// <summary>Log loss of this single prediction once settled. Null while unsettled or unpublished.</summary>
    public double? LogLoss
    {
        get
        {
            if (ActualOutcome is null || HomeProbability is null) return null;
            var p = ActualOutcome switch
            {
                Outcome.HomeWin => HomeProbability.Value,
                Outcome.Draw => DrawProbability!.Value,
                _ => AwayProbability!.Value
            };
            return -Math.Log(Math.Max(p, 1e-15));
        }
    }

    private static string N(double? v) => v.HasValue
        ? v.Value.ToString("0.00000000", CultureInfo.InvariantCulture) : string.Empty;
    private static string Q(string s) => s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    public static string CsvHeader =>
        "MatchId,PredictionTimestamp,EvidenceCutoff,ModelVersion,TeamStrengthVersion," +
        "HomeProbability,DrawProbability,AwayProbability,ConfidenceClass,GateStatus," +
        "ActualOutcome,HomeGoals,AwayGoals,SettledOn,LogLoss";

    public string ToCsv() => string.Join(',',
        Q(MatchId),
        PredictionTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        EvidenceCutoff?.ToString("yyyy-MM-dd") ?? string.Empty,
        Q(ModelVersion), Q(TeamStrengthVersion),
        N(HomeProbability), N(DrawProbability), N(AwayProbability),
        ConfidenceClass.ToString().ToUpperInvariant(), Q(GateStatus),
        ActualOutcome?.ToString() ?? string.Empty,
        HomeGoals?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        AwayGoals?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        SettledOn?.ToString("yyyy-MM-dd") ?? string.Empty,
        N(LogLoss));

    public static PredictionLogRecord From(PredictionDto dto) => new()
    {
        MatchId = dto.MatchId,
        PredictionTimestamp = dto.PredictionTimestamp,
        EvidenceCutoff = dto.EvidenceCutoff,
        ModelVersion = dto.ModelVersion,
        TeamStrengthVersion = dto.TeamStrengthVersion,
        HomeProbability = dto.HomeProbability,
        DrawProbability = dto.DrawProbability,
        AwayProbability = dto.AwayProbability,
        ConfidenceClass = dto.ConfidenceClass,
        GateStatus = dto.PredictionEligible ? "ELIGIBLE" : dto.GateReason
    };
}

/// <summary>Joins finished results onto logged predictions. Refuses to settle a match twice or settle it early.</summary>
public static class PredictionSettlement
{
    public sealed class Report
    {
        public int Settled;
        public int AlreadySettled;
        public int NoResultYet;
        public int RejectedAsEarly;
    }

    public static Report Settle(
        IEnumerable<PredictionLogRecord> log,
        IReadOnlyDictionary<string, (Outcome outcome, int hg, int ag, DateOnly date)> results)
    {
        var report = new Report();
        foreach (var row in log)
        {
            if (row.ActualOutcome is not null) { report.AlreadySettled++; continue; }
            if (!results.TryGetValue(row.MatchId, out var r)) { report.NoResultYet++; continue; }

            // a result may never be attached before the match it belongs to was predicted
            if (r.date < DateOnly.FromDateTime(row.PredictionTimestamp)) { report.RejectedAsEarly++; continue; }

            row.ActualOutcome = r.outcome;
            row.HomeGoals = r.hg;
            row.AwayGoals = r.ag;
            row.SettledOn = r.date;
            report.Settled++;
        }
        return report;
    }
}
