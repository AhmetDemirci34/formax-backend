using System.Globalization;
using Formax.DixonColes.Models;

namespace Formax.DixonColes.Services;

/// <summary>Proper scoring rules for a 3-class ordered outcome. No calibration is applied.</summary>
public sealed class MetricAccumulator
{
    public string Scope { get; }
    public string Group { get; }
    public string ModelVersion { get; }

    public int N { get; private set; }
    private double _logLoss, _brier, _rps;
    private int _correct;
    private int _actualHome, _actualDraw, _actualAway;
    private double _predHome, _predDraw, _predAway;

    public MetricAccumulator(string modelVersion, string scope, string group)
    { ModelVersion = modelVersion; Scope = scope; Group = group; }

    public void Add(ProbTriple p, Outcome actual)
    {
        N++;

        var pActual = p[actual];
        _logLoss += -Math.Log(Math.Max(pActual, 1e-15));

        // multiclass Brier: sum over classes of (p - y)^2  (range 0..2)
        var yH = actual == Outcome.HomeWin ? 1.0 : 0.0;
        var yD = actual == Outcome.Draw ? 1.0 : 0.0;
        var yA = actual == Outcome.AwayWin ? 1.0 : 0.0;
        _brier += (p.Home - yH) * (p.Home - yH) + (p.Draw - yD) * (p.Draw - yD) + (p.Away - yA) * (p.Away - yA);

        // ranked probability score over the ordered classes H < D < A, normalised by (k-1)=2
        var cumP1 = p.Home;
        var cumP2 = p.Home + p.Draw;
        var cumY1 = yH;
        var cumY2 = yH + yD;
        _rps += ((cumP1 - cumY1) * (cumP1 - cumY1) + (cumP2 - cumY2) * (cumP2 - cumY2)) / 2.0;

        if (p.ArgMax == actual) _correct++;

        if (actual == Outcome.HomeWin) _actualHome++;
        else if (actual == Outcome.Draw) _actualDraw++;
        else _actualAway++;
        _predHome += p.Home; _predDraw += p.Draw; _predAway += p.Away;
    }

    public double LogLoss => N == 0 ? double.NaN : _logLoss / N;
    public double Brier => N == 0 ? double.NaN : _brier / N;
    public double Rps => N == 0 ? double.NaN : _rps / N;
    public double Accuracy => N == 0 ? double.NaN : (double)_correct / N;
    public double ActualHomeRate => N == 0 ? double.NaN : (double)_actualHome / N;
    public double ActualDrawRate => N == 0 ? double.NaN : (double)_actualDraw / N;
    public double ActualAwayRate => N == 0 ? double.NaN : (double)_actualAway / N;
    public double MeanPredHome => N == 0 ? double.NaN : _predHome / N;
    public double MeanPredDraw => N == 0 ? double.NaN : _predDraw / N;
    public double MeanPredAway => N == 0 ? double.NaN : _predAway / N;

    private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.000000", CultureInfo.InvariantCulture);

    public static string CsvHeader =>
        "ModelVersion,Scope,Group,N,LogLoss,Brier,RPS,Accuracy," +
        "MeanPredHome,MeanPredDraw,MeanPredAway,ActualHomeRate,ActualDrawRate,ActualAwayRate";

    public string ToCsv()
    {
        static string Q(string s) => s.Contains(',') ? "\"" + s + "\"" : s;
        return string.Join(',', Q(ModelVersion), Q(Scope), Q(Group), N.ToString(CultureInfo.InvariantCulture),
            F(LogLoss), F(Brier), F(Rps), F(Accuracy),
            F(MeanPredHome), F(MeanPredDraw), F(MeanPredAway),
            F(ActualHomeRate), F(ActualDrawRate), F(ActualAwayRate));
    }
}

/// <summary>Reliability band: how often did an outcome happen when we said p was in this band.</summary>
public sealed class ReliabilityBand
{
    public string ModelVersion { get; }
    public string Band { get; }
    public double Lower { get; }
    public double Upper { get; }
    public int N { get; private set; }
    private double _sumPred;
    private int _hits;

    public ReliabilityBand(string modelVersion, double lower, double upper)
    {
        ModelVersion = modelVersion; Lower = lower; Upper = upper;
        Band = $"{lower * 100:0}-{upper * 100:0}%";
    }

    public bool Contains(double p) => p >= Lower && (p < Upper || (Upper >= 1.0 && p <= 1.0));

    public void Add(double predicted, bool happened)
    {
        N++; _sumPred += predicted; if (happened) _hits++;
    }

    public double MeanPredicted => N == 0 ? double.NaN : _sumPred / N;
    public double ActualRate => N == 0 ? double.NaN : (double)_hits / N;
    public double Gap => N == 0 ? double.NaN : MeanPredicted - ActualRate;

    private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.000000", CultureInfo.InvariantCulture);

    public static string CsvHeader => "ModelVersion,Band,N,MeanPredictedProbability,ActualFrequency,Gap";
    public string ToCsv() => string.Join(',', ModelVersion, Band, N.ToString(CultureInfo.InvariantCulture),
        F(MeanPredicted), F(ActualRate), F(Gap));
}
