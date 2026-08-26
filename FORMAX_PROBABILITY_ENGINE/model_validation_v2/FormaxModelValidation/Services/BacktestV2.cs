using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.TeamStrength.Models;

namespace Formax.ModelValidation.Services;

/// <summary>
/// Expanding, leakage-safe competition context. The same rules as the V1 backtest (goal baselines
/// and outcome frequencies per CompetitionType, learned from strictly earlier days); it is
/// re-declared here only because the V1 type is internal to its assembly. The framework
/// equivalence test proves the two produce identical numbers.
/// </summary>
internal sealed class ContextState
{
    public double SumHomeGoals, SumAwayGoals;
    public int Matches;
    public int Home, Draw, Away;

    public double BaselineHome(DixonColesConfig c) =>
        Matches >= c.MinBaselineSamples ? SumHomeGoals / Matches : c.SeedBaselineHomeGoals;
    public double BaselineAway(DixonColesConfig c) =>
        Matches >= c.MinBaselineSamples ? SumAwayGoals / Matches : c.SeedBaselineAwayGoals;

    public (double h, double d, double a) Frequencies(DixonColesConfig c)
    {
        if (Matches < c.MinFrequencySamples) return (c.SeedHomeRate, c.SeedDrawRate, c.SeedAwayRate);
        return ((double)Home / Matches, (double)Draw / Matches, (double)Away / Matches);
    }

    public void Apply(MatchRecord m)
    {
        SumHomeGoals += m.HomeGoals; SumAwayGoals += m.AwayGoals; Matches++;
        if (m.HomeGoals > m.AwayGoals) Home++;
        else if (m.HomeGoals == m.AwayGoals) Draw++;
        else Away++;
    }
}

/// <summary>The parameters under validation that live OUTSIDE the team strength engine.</summary>
public sealed class ModelParameters
{
    /// <summary>Dixon-Coles low-score dependence.</summary>
    public double Rho { get; set; } = -0.05;
    /// <summary>How the bivariate Poisson sets its shared component.</summary>
    public BivariateMode BivariateMode { get; set; } = BivariateMode.Proportional;
    /// <summary>The bivariate dependence parameter c. 0 = exactly independent Poisson.</summary>
    public double BivariateC { get; set; } = 0.0;

    public ModelParameters Clone() => new()
    { Rho = Rho, BivariateMode = BivariateMode, BivariateC = BivariateC };
}

public sealed class BacktestReport
{
    public int MatchesPredicted;
    public int MatchesSkippedNoSnapshot;
    public int LeakageViolations;
    public int NormalisationViolations;
}

/// <summary>
/// Walk-forward backtest, V2. The replay is the same as the V1 backtest - the same day-by-day
/// loop, the same inputs, the same lambdas - with three additions:
///   * the bivariate Poisson model,
///   * a segment label per prediction (TRAIN / VALIDATION / TEST),
///   * an optional test fence, so a parameter search physically cannot score a test match.
///
/// Time is replayed day by day. For every match the models see ONLY the team strength snapshot
/// produced before that match plus competition context accumulated from STRICTLY EARLIER days.
/// The results of a day are folded into the context only after every prediction of that day is made.
/// </summary>
public sealed class BacktestV2
{
    private readonly DixonColesConfig _cfg;
    private readonly DixonColesConfig _zeroRho;
    private readonly DixonColesConfig _dcRho;
    private readonly ModelParameters _p;
    private readonly SplitConfig _split;
    private readonly Dictionary<string, ContextState> _ctx = new(StringComparer.Ordinal);

    public BacktestV2(DixonColesConfig cfg, ModelParameters p, SplitConfig split)
    {
        _cfg = cfg;
        _p = p;
        _split = split;
        _zeroRho = Variant(cfg, 0.0);
        _dcRho = Variant(cfg, p.Rho);
    }

    private static DixonColesConfig Variant(DixonColesConfig cfg, double rho) => new()
    {
        Rho = rho,
        MaxGoals = cfg.MaxGoals,
        MinLambda = cfg.MinLambda,
        MaxLambda = cfg.MaxLambda,
        ProbabilityFloor = cfg.ProbabilityFloor
    };

    private ContextState Ctx(string competitionType)
    {
        if (!_ctx.TryGetValue(competitionType, out var c)) { c = new ContextState(); _ctx[competitionType] = c; }
        return c;
    }

    public BacktestReport Run(
        IReadOnlyList<MatchRecord> matches,
        IReadOnlyDictionary<(string matchId, string side), StrengthRow> snapshots,
        ModelMask mask,
        Action<MatchPrediction> sink,
        TestFence? fence = null)
    {
        var res = new BacktestReport();

        var ordered = matches
            .OrderBy(m => m.Date.DayNumber)
            .ThenBy(m => m.MatchId, StringComparer.Ordinal)
            .ToList();

        var i = 0;
        while (i < ordered.Count)
        {
            var day = ordered[i].Date;
            var j = i;
            while (j < ordered.Count && ordered[j].Date == day) j++;

            // ---- predict every match of the day from state that predates the day
            for (var k = i; k < j; k++)
            {
                var m = ordered[k];
                if (!snapshots.TryGetValue((m.MatchId, "HOME"), out var sh) ||
                    !snapshots.TryGetValue((m.MatchId, "AWAY"), out var sa))
                { res.MatchesSkippedNoSnapshot++; continue; }

                // leakage guard on the inputs themselves
                if ((sh.LastMatchDate.HasValue && sh.LastMatchDate.Value >= m.Date) ||
                    (sa.LastMatchDate.HasValue && sa.LastMatchDate.Value >= m.Date) ||
                    sh.MatchDate != m.Date || sa.MatchDate != m.Date)
                    res.LeakageViolations++;

                fence?.RecordScored(m.MatchId, m.Date);

                var ctx = Ctx(m.CompetitionType);
                var baseHome = ctx.BaselineHome(_cfg);
                var baseAway = ctx.BaselineAway(_cfg);

                var lambdaHome = Math.Clamp(baseHome * sh.Attack * sa.Defense, _cfg.MinLambda, _cfg.MaxLambda);
                var lambdaAway = Math.Clamp(baseAway * sa.Attack * sh.Defense, _cfg.MinLambda, _cfg.MaxLambda);

                var probs = new ProbTriple[ModelIds.All.Length];
                var lambda3 = 0.0;

                if ((mask & ModelMask.Simple) != 0)
                {
                    var (fh, fd, fa) = ctx.Frequencies(_cfg);
                    probs[(int)ModelId.Simple] = new ProbTriple(fh, fd, fa, _cfg.ProbabilityFloor);
                }

                if ((mask & ModelMask.TeamStrength) != 0)
                {
                    var (_, fd, _) = ctx.Frequencies(_cfg);
                    var sHome = Math.Max(sh.Overall * (baseHome / Math.Max(baseAway, 1e-9)), 1e-9);
                    var sAway = Math.Max(sa.Overall, 1e-9);
                    var pDraw = Math.Clamp(fd, 0.05, 0.60);
                    var decided = 1.0 - pDraw;
                    var pHome = decided * (sHome / (sHome + sAway));
                    var pAway = decided - pHome;
                    probs[(int)ModelId.TeamStrength] = new ProbTriple(pHome, pDraw, pAway, _cfg.ProbabilityFloor);
                }

                if ((mask & ModelMask.IndependentPoisson) != 0)
                    probs[(int)ModelId.IndependentPoisson] =
                        DixonColesModel.Outcome1X2(lambdaHome, lambdaAway, _zeroRho);

                if ((mask & ModelMask.DixonColes) != 0)
                {
                    var dc = DixonColesModel.Outcome1X2(lambdaHome, lambdaAway, _dcRho);
                    if (Math.Abs(dc.Sum - 1.0) > 1e-9) res.NormalisationViolations++;
                    probs[(int)ModelId.DixonColes] = dc;
                }

                if ((mask & ModelMask.BivariatePoisson) != 0)
                {
                    lambda3 = BivariatePoissonModel.SharedComponent(
                        lambdaHome, lambdaAway, _p.BivariateMode, _p.BivariateC);
                    var bp = BivariatePoissonModel.Outcome1X2(lambdaHome, lambdaAway, lambda3, _cfg);
                    if (Math.Abs(bp.Sum - 1.0) > 1e-9) res.NormalisationViolations++;
                    probs[(int)ModelId.BivariatePoisson] = bp;
                }

                sink(new MatchPrediction
                {
                    MatchId = m.MatchId,
                    Date = m.Date,
                    Segment = _split.Of(m.Date),
                    Season = m.Season,
                    Competition = m.Competition,
                    CompetitionType = m.CompetitionType,
                    HomeTeam = m.HomeTeamName,
                    AwayTeam = m.AwayTeamName,
                    LambdaHome = lambdaHome,
                    LambdaAway = lambdaAway,
                    Lambda3 = lambda3,
                    Probabilities = probs,
                    Actual = m.HomeGoals > m.AwayGoals ? Outcome.HomeWin
                           : m.HomeGoals == m.AwayGoals ? Outcome.Draw : Outcome.AwayWin,
                    HomeGoals = m.HomeGoals,
                    AwayGoals = m.AwayGoals,
                    HomeColdStartClass = sh.ColdStartClass,
                    AwayColdStartClass = sa.ColdStartClass,
                    HomePriorWeight = sh.PriorWeight,
                    AwayPriorWeight = sa.PriorWeight,
                    HomePriorSource = sh.PriorSource,
                    AwayPriorSource = sa.PriorSource,
                    IsColdStart = sh.MatchesUsed == 0 || sa.MatchesUsed == 0,
                    EvidenceCutoff = Later(sh.LastMatchDate, sa.LastMatchDate)
                });

                res.MatchesPredicted++;
            }

            // ---- only now the day's results become known to the context
            for (var k = i; k < j; k++) Ctx(ordered[k].CompetitionType).Apply(ordered[k]);
            i = j;
        }
        return res;
    }

    private static DateOnly? Later(DateOnly? a, DateOnly? b)
        => a is null ? b : b is null ? a : (a.Value > b.Value ? a : b);
}
