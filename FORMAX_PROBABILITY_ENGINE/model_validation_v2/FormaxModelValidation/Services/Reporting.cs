using System.Globalization;
using Formax.DixonColes.Services;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;

namespace Formax.ModelValidation.Services;

/// <summary>One measured cell: a parameter set, a model, a segment, a slice of the data.</summary>
public sealed class MetricRow
{
    /// <summary>VALIDATED_V2 or UNVALIDATED_V1 - which set of parameters produced this number.</summary>
    public required string ParameterSet { get; init; }
    public required string Segment { get; init; }
    public required string Scope { get; init; }
    public required string Group { get; init; }
    public required MetricAccumulator Acc { get; init; }

    private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);

    public static string CsvHeader =>
        "ParameterSet,Segment,Scope,Group,ModelVersion,N,LogLoss,Brier,RPS,Accuracy," +
        "MeanPredHome,MeanPredDraw,MeanPredAway,ActualHomeRate,ActualDrawRate,ActualAwayRate";

    public string ToCsv()
    {
        static string Q(string s) => s.Contains(',') ? "\"" + s + "\"" : s;
        return string.Join(',', Q(ParameterSet), Q(Segment), Q(Scope), Q(Group), Q(Acc.ModelVersion),
            Acc.N.ToString(CultureInfo.InvariantCulture),
            F(Acc.LogLoss), F(Acc.Brier), F(Acc.Rps), F(Acc.Accuracy),
            F(Acc.MeanPredHome), F(Acc.MeanPredDraw), F(Acc.MeanPredAway),
            F(Acc.ActualHomeRate), F(Acc.ActualDrawRate), F(Acc.ActualAwayRate));
    }
}

public static class Reporting
{
    public static string SegName(Segment s) => s.ToString().ToUpperInvariant();

    /// <summary>Scores every model on one slice of predictions.</summary>
    public static List<MetricRow> Score(
        IEnumerable<MatchPrediction> preds, string parameterSet, string segment, string scope, string group,
        IEnumerable<ModelId>? models = null)
    {
        var list = preds as IList<MatchPrediction> ?? preds.ToList();
        var rows = new List<MetricRow>();
        foreach (var m in models ?? ModelIds.All)
        {
            var acc = new MetricAccumulator(ModelIds.Name(m), scope, group);
            foreach (var p in list) acc.Add(p.Probabilities[(int)m], p.Actual);
            rows.Add(new MetricRow
            { ParameterSet = parameterSet, Segment = segment, Scope = scope, Group = group, Acc = acc });
        }
        return rows;
    }

    /// <summary>Scores every model on every group of a slicing key, for one segment.</summary>
    public static List<MetricRow> ScoreBy(
        IEnumerable<MatchPrediction> preds, string parameterSet, string segment, string scope,
        Func<MatchPrediction, string> key)
    {
        var rows = new List<MetricRow>();
        foreach (var g in preds.GroupBy(key).OrderBy(g => g.Key, StringComparer.Ordinal))
            rows.AddRange(Score(g.ToList(), parameterSet, segment, scope, g.Key));
        return rows;
    }

    public static void WriteMetrics(string path, IEnumerable<MetricRow> rows)
    {
        using var w = new StreamWriter(path, false);
        w.WriteLine(MetricRow.CsvHeader);
        foreach (var r in rows) w.WriteLine(r.ToCsv());
    }

    public static void WriteLines(string path, string header, IEnumerable<string> lines)
    {
        using var w = new StreamWriter(path, false);
        w.WriteLine(header);
        foreach (var l in lines) w.WriteLine(l);
    }

    public static double LogLossOf(IEnumerable<MatchPrediction> preds, ModelId m)
    {
        double sum = 0; var n = 0;
        foreach (var p in preds)
        {
            sum += -Math.Log(Math.Max(p.Probabilities[(int)m][p.Actual], 1e-15));
            n++;
        }
        return n == 0 ? double.NaN : sum / n;
    }
}
