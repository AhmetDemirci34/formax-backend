using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.TeamStrength.Models;

namespace Formax.DixonColes.Services;

/// <summary>Expanding, leakage-safe context learned per CompetitionType.</summary>
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

public sealed class BacktestResult
{
    /// <summary>modelVersion -> predictions, in chronological order.</summary>
    public Dictionary<string, List<Prediction>> ByModel { get; } = new();
    public int MatchesPredicted { get; set; }
    public int MatchesSkippedNoSnapshot { get; set; }
    public int LeakageViolations { get; set; }
    public int NormalisationViolations { get; set; }
}

/// <summary>
/// Walk-forward backtest.
///
/// Time is replayed day by day. For every match the models see ONLY:
///   * the team strength snapshot produced before that match (LastMatchDate is strictly earlier), and
///   * competition context accumulated from matches played on STRICTLY EARLIER days.
/// The results of a day are folded into the context only AFTER every prediction of that day is made,
/// so two matches on the same date can never inform each other.
///
/// Four models are scored on exactly the same split:
///   SIMPLE_BASELINE_V1        expanding outcome frequency of the competition type, no team info
///   TEAM_STRENGTH_BASELINE_V1 team strength ratio + empirical draw rate, no goal model
///   INDEPENDENT_POISSON_V1    same lambdas as Dixon-Coles, rho = 0
///   DIXON_COLES_BASELINE_V1   same lambdas with the Dixon-Coles low-score correction
/// </summary>
public sealed class WalkForwardBacktest
{
    public const string SimpleModel = "SIMPLE_BASELINE_V1";
    public const string StrengthModel = "TEAM_STRENGTH_BASELINE_V1";
    public const string PoissonModel = "INDEPENDENT_POISSON_V1";
    public const string DixonColesModelName = "DIXON_COLES_BASELINE_V1";

    private readonly DixonColesConfig _cfg;
    private readonly Dictionary<string, ContextState> _ctx = new(StringComparer.Ordinal);

    public WalkForwardBacktest(DixonColesConfig cfg) => _cfg = cfg;

    private ContextState Ctx(string competitionType)
    {
        if (!_ctx.TryGetValue(competitionType, out var c)) { c = new ContextState(); _ctx[competitionType] = c; }
        return c;
    }

    public BacktestResult Run(
        IReadOnlyList<MatchRecord> matches,
        Dictionary<(string matchId, string side), StrengthRow> snapshots)
    {
        var res = new BacktestResult();
        foreach (var v in new[] { SimpleModel, StrengthModel, PoissonModel, DixonColesModelName })
            res.ByModel[v] = new List<Prediction>();

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

                var ctx = Ctx(m.CompetitionType);
                var baseHome = ctx.BaselineHome(_cfg);
                var baseAway = ctx.BaselineAway(_cfg);

                var lambdaHome = Math.Clamp(baseHome * sh.Attack * sa.Defense, _cfg.MinLambda, _cfg.MaxLambda);
                var lambdaAway = Math.Clamp(baseAway * sa.Attack * sh.Defense, _cfg.MinLambda, _cfg.MaxLambda);

                var actual = m.HomeGoals > m.AwayGoals ? Outcome.HomeWin
                           : m.HomeGoals == m.AwayGoals ? Outcome.Draw : Outcome.AwayWin;
                var evidenceCutoff = Later(sh.LastMatchDate, sa.LastMatchDate);

                // 1) naive: expanding outcome frequency of this competition type
                var (fh, fd, fa) = ctx.Frequencies(_cfg);
                Add(res, SimpleModel, m, new ProbTriple(fh, fd, fa, _cfg.ProbabilityFloor), 0, 0, actual, sh, sa, evidenceCutoff);

                // 2) team strength only: strength ratio for the decided part, empirical draw rate for the rest
                var sHome = Math.Max(sh.Overall * (baseHome / Math.Max(baseAway, 1e-9)), 1e-9);
                var sAway = Math.Max(sa.Overall, 1e-9);
                var pDraw = Math.Clamp(fd, 0.05, 0.60);
                var decided = 1.0 - pDraw;
                var pHome = decided * (sHome / (sHome + sAway));
                var pAway = decided - pHome;
                Add(res, StrengthModel, m, new ProbTriple(pHome, pDraw, pAway, _cfg.ProbabilityFloor), lambdaHome, lambdaAway, actual, sh, sa, evidenceCutoff);

                // 3) independent Poisson on the same lambdas
                Add(res, PoissonModel, m, DixonColes.Services.DixonColesModel.Outcome1X2IndependentPoisson(lambdaHome, lambdaAway, _cfg),
                    lambdaHome, lambdaAway, actual, sh, sa, evidenceCutoff);

                // 4) Dixon-Coles
                var dc = DixonColes.Services.DixonColesModel.Outcome1X2(lambdaHome, lambdaAway, _cfg);
                if (Math.Abs(dc.Sum - 1.0) > 1e-9) res.NormalisationViolations++;
                Add(res, DixonColesModelName, m, dc, lambdaHome, lambdaAway, actual, sh, sa, evidenceCutoff);

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

    private void Add(BacktestResult res, string model, MatchRecord m, ProbTriple p,
        double lh, double la, Outcome actual, StrengthRow sh, StrengthRow sa, DateOnly? cutoff)
    {
        res.ByModel[model].Add(new Prediction
        {
            MatchId = m.MatchId,
            PredictionDate = m.Date,
            ModelVersion = model,
            TeamStrengthVersion = _cfg.TeamStrengthVersion,
            ConfigVersion = _cfg.ConfigVersion,
            Season = m.Season,
            Competition = m.Competition,
            CompetitionType = m.CompetitionType,
            HomeTeam = m.HomeTeamName,
            AwayTeam = m.AwayTeamName,
            Probabilities = p,
            LambdaHome = lh,
            LambdaAway = la,
            Actual = actual,
            HomeGoals = m.HomeGoals,
            AwayGoals = m.AwayGoals,
            HomeColdStartClass = sh.ColdStartClass,
            AwayColdStartClass = sa.ColdStartClass,
            HomeConfidence = sh.Confidence,
            AwayConfidence = sa.Confidence,
            HomePriorWeight = sh.PriorWeight,
            AwayPriorWeight = sa.PriorWeight,
            HomePriorSource = sh.PriorSource,
            AwayPriorSource = sa.PriorSource,
            IsColdStart = sh.MatchesUsed == 0 || sa.MatchesUsed == 0,
            EvidenceCutoff = cutoff
        });
    }
}
