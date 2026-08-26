using System.Globalization;
using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;

namespace Formax.ModelValidation.Services;

/// <summary>One evaluated candidate. Every row the search ever looked at ends up in the trace.</summary>
public sealed class TuningRecord
{
    public required int Seq { get; init; }
    public required string Stage { get; init; }
    public required string SweptParameter { get; init; }
    public required string EvaluatedOn { get; init; }
    public required string SelectionModel { get; init; }

    public required double HalfLifeDays { get; init; }
    public required double LearningRate { get; init; }
    public required double ShrinkageK { get; init; }
    public required double RatioSmoothing { get; init; }
    public required double VenueShrinkageK { get; init; }
    public required double MinIndex { get; init; }
    public required double MaxIndex { get; init; }
    public required int MinBaselineSamples { get; init; }
    public required bool UseCompetitionTypePool { get; init; }
    public required double Rho { get; init; }
    public required string BivariateMode { get; init; }
    public required double BivariateC { get; init; }

    public required int N { get; init; }
    public required double LogLoss { get; init; }
    public required double Brier { get; init; }
    public required double Rps { get; init; }
    public required double Accuracy { get; init; }
    public bool Selected { get; set; }

    private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.00000000", CultureInfo.InvariantCulture);

    public static string CsvHeader =>
        "Seq,Stage,SweptParameter,EvaluatedOn,SelectionModel," +
        "halfLifeDays,learningRate,shrinkageK,ratioSmoothing,venueShrinkageK,minIndex,maxIndex," +
        "minBaselineSamples,useCompetitionTypePool,rho,bivariateMode,bivariateC," +
        "N,LogLoss,Brier,RPS,Accuracy,Selected";

    public string ToCsv() => string.Join(',',
        Seq.ToString(CultureInfo.InvariantCulture), Stage, SweptParameter, EvaluatedOn, SelectionModel,
        F(HalfLifeDays), F(LearningRate), F(ShrinkageK), F(RatioSmoothing), F(VenueShrinkageK),
        F(MinIndex), F(MaxIndex),
        MinBaselineSamples.ToString(CultureInfo.InvariantCulture),
        UseCompetitionTypePool ? "True" : "False",
        F(Rho), BivariateMode, F(BivariateC),
        N.ToString(CultureInfo.InvariantCulture), F(LogLoss), F(Brier), F(Rps), F(Accuracy),
        Selected ? "True" : "False");
}

/// <summary>A lambda pair plus its realised score, cached so rho and the bivariate c can be swept without replaying history.</summary>
public readonly struct LambdaSample
{
    public LambdaSample(double lh, double la, int hg, int ag, Segment seg)
    { Lh = lh; La = la; Hg = hg; Ag = ag; Seg = seg; }
    public double Lh { get; }
    public double La { get; }
    public int Hg { get; }
    public int Ag { get; }
    public Segment Seg { get; }
    public Outcome Actual => Hg > Ag ? Outcome.HomeWin : Hg == Ag ? Outcome.Draw : Outcome.AwayWin;
}

public sealed class ScoredCandidate
{
    public required TeamStrengthConfig Strength { get; init; }
    public required ModelParameters Parameters { get; init; }
    public required MetricAccumulator Metrics { get; init; }
    public MetricAccumulator? TeamStrengthMetrics { get; init; }
}

/// <summary>
/// The parameter search.
///
/// PROTOCOL - fixed before the first candidate ran, and not changed afterwards:
///   Stage 1  team strength: a full grid over the three primary parameters
///            (halfLifeDays x learningRate x shrinkageK)
///   Stage 2  team strength: coordinate descent over the remaining parameters
///   Stage 3  repeat stage 1 at the stage 2 values; iterate until nothing moves
///   Stage 4  Dixon-Coles rho: swept on VALIDATION log loss, and independently estimated by
///            maximum likelihood on TRAIN scores; both reported
///   Stage 5  bivariate dependence c: swept the same way, for both parameterisations
///
/// Every stage is scored on VALIDATION only, with the selection model of the split config
/// (independent Poisson: the one model that consumes the lambdas directly and adds no free
/// parameter of its own). Every match that enters any of it passes the test fence first.
/// </summary>
public sealed class Tuner
{
    private readonly IReadOnlyList<MatchRecord> _selectable;
    private readonly SplitConfig _split;
    private readonly DixonColesConfig _dc;
    private readonly TestFence _fence;
    private readonly List<TuningRecord> _trace = new();
    private readonly object _traceGate = new();
    private int _seq;

    public IReadOnlyList<TuningRecord> Trace => _trace;
    public int Evaluations => _seq;

    public Tuner(IReadOnlyList<MatchRecord> allMatches, SplitConfig split, DixonColesConfig dc, TestFence fence)
    {
        _fence = fence;
        _split = split;
        _dc = dc;
        // The ONLY match list this class will ever see. Everything from the test boundary on is gone.
        _selectable = fence.Selectable(allMatches);
    }

    public int SelectableMatches => _selectable.Count;

    // ---------------------------------------------------------------- evaluation

    /// <summary>
    /// One candidate: rebuild team strength from scratch, replay the walk-forward, score VALIDATION.
    /// TRAIN matches are replayed (the rating has to be built) but never scored into the selection
    /// metric - they are the warm-up, not the judge.
    /// </summary>
    public ScoredCandidate Evaluate(TeamStrengthConfig ts, ModelParameters mp, ModelMask mask)
    {
        var rows = StrengthPipeline.BuildRows(ts, _selectable);

        var val = new MetricAccumulator(_split.SelectionModel, "VALIDATION", "all");
        var valTs = new MetricAccumulator("TEAM_STRENGTH", "VALIDATION", "all");
        var selected = SelectionModelId(mask);

        new BacktestV2(_dc, mp, _split).Run(_selectable, rows, mask, p =>
        {
            if (p.Segment != Segment.Validation) return;
            val.Add(p.Probabilities[(int)selected], p.Actual);
            if ((mask & ModelMask.TeamStrength) != 0)
                valTs.Add(p.Probabilities[(int)ModelId.TeamStrength], p.Actual);
        }, _fence);

        return new ScoredCandidate
        {
            Strength = ts,
            Parameters = mp,
            Metrics = val,
            TeamStrengthMetrics = (mask & ModelMask.TeamStrength) != 0 ? valTs : null
        };
    }

    private ModelId SelectionModelId(ModelMask mask) => _split.SelectionModel switch
    {
        "TEAM_STRENGTH" => ModelId.TeamStrength,
        "DIXON_COLES" => ModelId.DixonColes,
        "BIVARIATE_POISSON" => ModelId.BivariatePoisson,
        _ => ModelId.IndependentPoisson
    };

    /// <summary>Replays once and caches every lambda pair, so rho and c can be swept without rebuilding anything.</summary>
    public List<LambdaSample> CacheLambdas(TeamStrengthConfig ts)
    {
        var rows = StrengthPipeline.BuildRows(ts, _selectable);
        var cache = new List<LambdaSample>(_selectable.Count);
        new BacktestV2(_dc, new ModelParameters(), _split).Run(_selectable, rows, ModelMask.None,
            p => cache.Add(new LambdaSample(p.LambdaHome, p.LambdaAway, p.HomeGoals, p.AwayGoals, p.Segment)),
            _fence);
        return cache;
    }

    // ---------------------------------------------------------------- search

    private void Record(string stage, string swept, TeamStrengthConfig ts, ModelParameters mp,
        MetricAccumulator m, string? selectionModelOverride = null)
    {
        lock (_traceGate)
        {
            _trace.Add(new TuningRecord
            {
                Seq = ++_seq,
                Stage = stage,
                SweptParameter = swept,
                EvaluatedOn = "VALIDATION",
                SelectionModel = selectionModelOverride ?? _split.SelectionModel,
                HalfLifeDays = ts.HalfLifeDays,
                LearningRate = ts.LearningRate,
                ShrinkageK = ts.ShrinkageK,
                RatioSmoothing = ts.RatioSmoothing,
                VenueShrinkageK = ts.VenueShrinkageK,
                MinIndex = ts.MinIndex,
                MaxIndex = ts.MaxIndex,
                MinBaselineSamples = ts.MinBaselineSamples,
                UseCompetitionTypePool = ts.UseCompetitionTypePool,
                Rho = mp.Rho,
                BivariateMode = mp.BivariateMode.ToString(),
                BivariateC = mp.BivariateC,
                N = m.N,
                LogLoss = m.LogLoss,
                Brier = m.Brier,
                Rps = m.Rps,
                Accuracy = m.Accuracy
            });
        }
    }

    /// <summary>Marks the winning row of a stage, so the trace shows what was picked and not only what was tried.</summary>
    public void MarkSelected(string stage, Func<TuningRecord, bool> match)
    {
        var row = _trace.Where(r => r.Stage == stage && match(r)).OrderBy(r => r.Seq).FirstOrDefault();
        if (row is not null) row.Selected = true;
    }

    /// <summary>Evaluates a batch of candidates in parallel and returns them ordered by the selection metric.</summary>
    public List<(TeamStrengthConfig cfg, ScoredCandidate scored)> Sweep(
        string stage, string swept, IEnumerable<TeamStrengthConfig> candidates, ModelParameters mp, ModelMask mask)
    {
        var list = candidates.ToList();
        var results = new (TeamStrengthConfig, ScoredCandidate)[list.Count];

        Parallel.For(0, list.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            i =>
            {
                var scored = Evaluate(list[i], mp, mask);
                results[i] = (list[i], scored);
            });

        for (var i = 0; i < list.Count; i++)
            Record(stage, swept, list[i], mp, results[i].Item2.Metrics);

        return results.OrderBy(r => r.Item2.Metrics.LogLoss).ToList();
    }
}
