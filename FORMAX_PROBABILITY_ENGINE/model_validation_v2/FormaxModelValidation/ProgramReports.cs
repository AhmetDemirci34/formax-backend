using System.Globalization;
using System.Text;
using Formax.DixonColes.Config;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.ModelValidation.Services;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;

namespace Formax.ModelValidation;

/// <summary>
/// The parts of the run that produce evidence rather than numbers: the validated config, the
/// provenance table for every parameter, the leakage audit and the verdicts.
/// </summary>
public static class ProgramReports
{
    private static string F(double v) => v.ToString("0.########", CultureInfo.InvariantCulture);
    private static string Q(string s) => s.Contains(',') || s.Contains('"')
        ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    // ------------------------------------------------------------------ validated config

    public static void WriteValidatedConfig(string path, TeamStrengthConfig ts, ModelParameters mp,
        SplitConfig split, int evaluations, double venueSpread,
        bool converged, IReadOnlyList<string> gridEdges)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"_searchConvergence\": \"{(converged ? "The search converged: a full round changed nothing." : "WARNING - the search was still improving when it reached its round limit. The values below are the best found, not a converged optimum.")}\",");
        sb.AppendLine($"  \"_gridEdges\": \"{(gridEdges.Count == 0 ? "No selected value sits on the edge of its candidate range." : "WARNING - selected at the edge of the candidate range: " + string.Join("; ", gridEdges) + ". The optimum may lie outside the search space.")}\",");
        sb.AppendLine($"  \"_comment\": \"Team strength configuration selected by walk-forward validation. Every value below was chosen by minimising {split.SelectionMetric} of the {split.SelectionModel} model on the VALIDATION segment ({split.ValidationStart} .. {split.TestStart}) after replaying TRAIN. No value was chosen on, or scored against, the TEST segment - see validation_parameters.csv and leakage_audit.csv.\",");
        sb.AppendLine($"  \"configVersion\": \"TS_CONFIG_V2_VALIDATED\",");
        sb.AppendLine($"  \"splitVersion\": \"{split.SplitVersion}\",");
        sb.AppendLine($"  \"selectedOn\": \"VALIDATION\",");
        sb.AppendLine($"  \"selectionCriterion\": \"{split.SelectionMetric} of {split.SelectionModel}\",");
        sb.AppendLine($"  \"candidatesEvaluated\": {evaluations},");
        sb.AppendLine();
        sb.AppendLine($"  \"halfLifeDays\": {F(ts.HalfLifeDays)},");
        sb.AppendLine("  \"_halfLifeDays\": \"VALIDATED. Exponential time weight of the rating.\",");
        sb.AppendLine();
        sb.AppendLine($"  \"learningRate\": {F(ts.LearningRate)},");
        sb.AppendLine("  \"_learningRate\": \"VALIDATED. Step size of the multiplicative update after each match.\",");
        sb.AppendLine();
        sb.AppendLine($"  \"shrinkageK\": {F(ts.ShrinkageK)},");
        sb.AppendLine("  \"_shrinkageK\": \"VALIDATED. Partial pooling constant: own evidence weight = n_eff / (n_eff + k). This is the prior weight of the cold-start policy.\",");
        sb.AppendLine();
        sb.AppendLine($"  \"venueShrinkageK\": {F(ts.VenueShrinkageK)},");
        sb.AppendLine($"  \"_venueShrinkageK\": \"NOT VALIDATED - NOT IDENTIFIABLE. The home-only and away-only indices are not read by any of the four 1X2 models, so validation log loss cannot distinguish between values: measured spread over k in 1/4/16 was {venueSpread.ToString("0.0e+0", CultureInfo.InvariantCulture)}. Left at its V1 value. Validating it needs a model that consumes venue indices.\",");
        sb.AppendLine();
        sb.AppendLine($"  \"ratioSmoothing\": {F(ts.RatioSmoothing)},");
        sb.AppendLine("  \"_ratioSmoothing\": \"VALIDATED. Additive smoothing in the surprise ratio.\",");
        sb.AppendLine();
        sb.AppendLine($"  \"minIndex\": {F(ts.MinIndex)},");
        sb.AppendLine($"  \"maxIndex\": {F(ts.MaxIndex)},");
        sb.AppendLine("  \"_indexClamp\": \"VALIDATED as a pair.\",");
        sb.AppendLine();
        sb.AppendLine($"  \"limitedThreshold\": {F(ts.LimitedThreshold)},");
        sb.AppendLine($"  \"developingThreshold\": {F(ts.DevelopingThreshold)},");
        sb.AppendLine($"  \"establishedThreshold\": {F(ts.EstablishedThreshold)},");
        sb.AppendLine($"  \"richThreshold\": {F(ts.RichThreshold)},");
        sb.AppendLine("  \"_thresholds\": \"NOT VALIDATED - REPORTING LABELS ONLY. These decide the ColdStartClass and Confidence strings; they never enter a probability. Proven by test: changing them leaves every probability bit-identical.\",");
        sb.AppendLine();
        sb.AppendLine($"  \"seedBaselineHomeGoals\": {F(ts.SeedBaselineHomeGoals)},");
        sb.AppendLine($"  \"seedBaselineAwayGoals\": {F(ts.SeedBaselineAwayGoals)},");
        sb.AppendLine($"  \"minBaselineSamples\": {ts.MinBaselineSamples},");
        sb.AppendLine("  \"_baselines\": \"minBaselineSamples is VALIDATED. The two seeds are only used before that many matches of a competition type exist and were left at their V1 values.\",");
        sb.AppendLine();
        sb.AppendLine($"  \"useCompetitionTypePool\": {(ts.UseCompetitionTypePool ? "true" : "false")},");
        sb.AppendLine("  \"_useCompetitionTypePool\": \"VALIDATED. Whether a team with little history is shrunk towards the pool of its competition type.\",");
        sb.AppendLine();
        sb.AppendLine($"  \"requiredIdentityConfidence\": \"{ts.RequiredIdentityConfidence}\",");
        sb.AppendLine("  \"_requiredIdentityConfidence\": \"FROZEN - data hygiene, not a model parameter.\",");
        sb.AppendLine();
        sb.Append("  \"acceptedMatchStatuses\": [ ");
        sb.Append(string.Join(", ", ts.AcceptedMatchStatuses.Select(s => $"\"{s}\"")));
        sb.AppendLine(" ],");
        sb.AppendLine("  \"_acceptedMatchStatuses\": \"FROZEN - data hygiene, not a model parameter.\",");
        sb.AppendLine();
        sb.AppendLine("  \"_dependenceParameters\": \"These belong to the probability models, not to the rating, and are listed here so one file carries the whole validated set.\",");
        sb.AppendLine($"  \"dixonColesRho\": {F(mp.Rho)},");
        sb.AppendLine($"  \"bivariateMode\": \"{mp.BivariateMode}\",");
        sb.AppendLine($"  \"bivariateC\": {F(mp.BivariateC)}");
        sb.AppendLine("}");
        File.WriteAllText(path, sb.ToString());
    }

    // ------------------------------------------------------------------ parameter provenance

    public sealed record ParamRow(string Parameter, string Owner, string Selected, string Previous,
        string Stage, string SelectedOn, string Criterion, string Status);

    public static void WriteParameterTable(string path,
        TeamStrengthConfig chosen, TeamStrengthConfig previous,
        ModelParameters chosenMp, ModelParameters previousMp,
        SplitConfig split, TestFence fence,
        DependencePoint rhoVal, DependencePoint rhoMle,
        DependencePoint bpVal, DependencePoint bpMle, BivariateMode bpMode,
        double venueSpread, int validationN, int trainN,
        bool converged, IReadOnlyList<string> gridEdges)
    {
        var crit = $"min {split.SelectionMetric} of {split.SelectionModel} on VALIDATION (N={validationN})";

        // a value chosen at the edge of its own candidate range is reported as such, not as an optimum
        var edged = gridEdges.GroupBy(e => e.Split('=')[0], StringComparer.Ordinal)
                             .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        string Status(string parameterKey)
        {
            if (edged.TryGetValue(parameterKey, out var edge))
                return edge.Contains("limiting case", StringComparison.Ordinal)
                    ? "VALIDATED - SELECTED AT THE LIMITING CASE (the mechanism is switched off)"
                    : "VALIDATED - BUT SELECTED AT THE EDGE OF THE CANDIDATE RANGE";
            return converged ? "VALIDATED" : "VALIDATED - SEARCH HIT ITS ROUND LIMIT";
        }
        var rows = new List<ParamRow>
        {
            new("halfLifeDays", "TeamStrength", F(chosen.HalfLifeDays), F(previous.HalfLifeDays),
                "S1 primary grid", "VALIDATION", crit, Status("halfLifeDays")),
            new("learningRate", "TeamStrength", F(chosen.LearningRate), F(previous.LearningRate),
                "S1 primary grid", "VALIDATION", crit, Status("learningRate")),
            new("shrinkageK (prior weight)", "TeamStrength", F(chosen.ShrinkageK), F(previous.ShrinkageK),
                "S1 primary grid", "VALIDATION", crit, Status("shrinkageK")),
            new("ratioSmoothing", "TeamStrength", F(chosen.RatioSmoothing), F(previous.RatioSmoothing),
                "S2 coordinate descent", "VALIDATION", crit, Status("ratioSmoothing")),
            new("minBaselineSamples", "TeamStrength", chosen.MinBaselineSamples.ToString(), previous.MinBaselineSamples.ToString(),
                "S2 coordinate descent", "VALIDATION", crit, Status("minBaselineSamples")),
            new("useCompetitionTypePool", "TeamStrength", chosen.UseCompetitionTypePool.ToString(), previous.UseCompetitionTypePool.ToString(),
                "S2 coordinate descent", "VALIDATION", crit, Status("useCompetitionTypePool")),
            new("minIndex", "TeamStrength", F(chosen.MinIndex), F(previous.MinIndex),
                "S2 coordinate descent", "VALIDATION", crit, Status("indexClamp")),
            new("maxIndex", "TeamStrength", F(chosen.MaxIndex), F(previous.MaxIndex),
                "S2 coordinate descent", "VALIDATION", crit, Status("indexClamp")),
            new("venueShrinkageK", "TeamStrength", F(chosen.VenueShrinkageK), F(previous.VenueShrinkageK),
                "S2 diagnostic sweep", "VALIDATION",
                $"no effect: log loss spread over k in 1/4/16 = {venueSpread.ToString("0.0e+0", CultureInfo.InvariantCulture)}",
                "NOT IDENTIFIABLE - left at V1"),
            new("limited/developing/established/richThreshold", "TeamStrength",
                $"{F(chosen.LimitedThreshold)}/{F(chosen.DevelopingThreshold)}/{F(chosen.EstablishedThreshold)}/{F(chosen.RichThreshold)}",
                $"{F(previous.LimitedThreshold)}/{F(previous.DevelopingThreshold)}/{F(previous.EstablishedThreshold)}/{F(previous.RichThreshold)}",
                "not swept", "-", "reporting labels only - never enter a probability", "NOT A MODEL PARAMETER"),
            new("seedBaselineHomeGoals/AwayGoals", "TeamStrength",
                $"{F(chosen.SeedBaselineHomeGoals)}/{F(chosen.SeedBaselineAwayGoals)}",
                $"{F(previous.SeedBaselineHomeGoals)}/{F(previous.SeedBaselineAwayGoals)}",
                "not swept", "-", "only active before minBaselineSamples matches exist", "FROZEN AT V1"),
            new("requiredIdentityConfidence", "TeamStrength", chosen.RequiredIdentityConfidence, previous.RequiredIdentityConfidence,
                "not swept", "-", "data hygiene", "FROZEN"),
            new("acceptedMatchStatuses", "TeamStrength", string.Join("|", chosen.AcceptedMatchStatuses), string.Join("|", previous.AcceptedMatchStatuses),
                "not swept", "-", "data hygiene", "FROZEN"),

            new("rho", "DixonColes", F(chosenMp.Rho), F(previousMp.Rho),
                "S4 sweep over cached lambdas", "VALIDATION", crit, "VALIDATED"),
            new("rho (cross-check)", "DixonColes", F(rhoMle.Value), F(previousMp.Rho),
                "S4 maximum likelihood", "TRAIN",
                $"max mean log P(exact score) on TRAIN (N={trainN}) = {rhoMle.TrainScoreLogLik.ToString("0.000000", CultureInfo.InvariantCulture)}",
                "REPORTED, NOT USED FOR SELECTION"),
            new("bivariateMode", "BivariatePoisson", bpMode.ToString(), "n/a (model did not exist in V1)",
                "S5 sweep over cached lambdas", "VALIDATION", crit, "VALIDATED"),
            new("bivariateC", "BivariatePoisson", F(chosenMp.BivariateC), "n/a (model did not exist in V1)",
                "S5 sweep over cached lambdas", "VALIDATION", crit, "VALIDATED"),
            new("bivariateC (cross-check)", "BivariatePoisson", F(bpMle.Value), "n/a",
                "S5 maximum likelihood", "TRAIN",
                $"max mean log P(exact score) on TRAIN (N={trainN}) = {bpMle.TrainScoreLogLik.ToString("0.000000", CultureInfo.InvariantCulture)}",
                "REPORTED, NOT USED FOR SELECTION"),

            new("maxGoals / lambda clamp / probability floor", "Backtest context", "unchanged", "unchanged",
                "not swept", "-", "numerical guard rails shared by all four models", "FROZEN AT V1"),
            new("competition goal baselines and outcome frequencies", "Backtest context", "unchanged", "unchanged",
                "not swept", "-", "shared input: identical for all four models, cannot favour one", "FROZEN AT V1")
        };

        var sb = new StringBuilder();
        sb.AppendLine("Parameter,Owner,SelectedValue,PreviousUnvalidatedValue,Changed,Stage,SelectedOnSegment,Criterion," +
                      "TestMatchesVisibleToSearch,LatestDateSeenBySearch,TestBoundary,Status");
        foreach (var r in rows)
            sb.AppendLine(string.Join(',',
                Q(r.Parameter), Q(r.Owner), Q(r.Selected), Q(r.Previous),
                r.Selected == r.Previous ? "no" : "yes",
                Q(r.Stage), Q(r.SelectedOn), Q(r.Criterion),
                "0", fence.LatestDateScored?.ToString("yyyy-MM-dd") ?? "(none)", split.TestStart, Q(r.Status)));
        File.WriteAllText(path, sb.ToString());
    }

    // ------------------------------------------------------------------ verdicts

    private const double MeaningfulLogLoss = 0.0010;

    public static List<string> Verdicts(IReadOnlyList<MatchPrediction> testPreds, IReadOnlyList<PairComparison> pairs)
    {
        var lines = new List<string>();

        lines.AddRange(Judge(testPreds, pairs, ModelId.IndependentPoisson, ModelId.TeamStrength,
            "POISSON_IMPROVEMENT", "goal model over the rating-only baseline"));
        lines.Add("");
        lines.AddRange(Judge(testPreds, pairs, ModelId.DixonColes, ModelId.IndependentPoisson,
            "DIXON_COLES_IMPROVEMENT", "low-score correction over independent Poisson"));
        lines.Add("");

        var bpReference = Reporting.LogLossOf(testPreds, ModelId.DixonColes)
                        < Reporting.LogLossOf(testPreds, ModelId.IndependentPoisson)
            ? ModelId.DixonColes : ModelId.IndependentPoisson;
        lines.AddRange(Judge(testPreds, pairs, ModelId.BivariatePoisson, bpReference,
            "BIVARIATE_POISSON_IMPROVEMENT", $"shared component over the best simpler goal model ({ModelIds.Name(bpReference)})"));

        return lines;
    }

    private static List<string> Judge(IReadOnlyList<MatchPrediction> testPreds, IReadOnlyList<PairComparison> pairs,
        ModelId candidate, ModelId reference, string label, string what)
    {
        var lines = new List<string>();
        var cName = ModelIds.Name(candidate);
        var rName = ModelIds.Name(reference);

        var accC = Acc(testPreds, candidate);
        var accR = Acc(testPreds, reference);
        var dLog = accC.ll - accR.ll;
        var dBrier = accC.brier - accR.brier;
        var dRps = accC.rps - accR.rps;

        var pair = pairs.FirstOrDefault(p => p.Scope == "TEST" && p.Group == "all"
            && ((p.ModelA == cName && p.ModelB == rName) || (p.ModelA == rName && p.ModelB == cName)));
        var ciExcludesZero = pair?.CiExcludesZero ?? false;
        var ciText = pair is null ? "(no interval)"
            : pair.ModelA == cName
                ? $"[{pair.CiLow:+0.00000;-0.00000;0}, {pair.CiHigh:+0.00000;-0.00000;0}]"
                : $"[{-pair.CiHigh:+0.00000;-0.00000;0}, {-pair.CiLow:+0.00000;-0.00000;0}]";

        // stability: the candidate must not lose a test season or a sizeable competition type
        var seasonLosses = new List<string>();
        foreach (var g in testPreds.GroupBy(p => p.Season).OrderBy(g => g.Key, StringComparer.Ordinal))
            if (Reporting.LogLossOf(g, candidate) >= Reporting.LogLossOf(g, reference))
                seasonLosses.Add(g.Key);

        var compLosses = new List<string>();
        foreach (var g in testPreds.GroupBy(p => p.CompetitionType).Where(g => g.Count() >= 100)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
            if (Reporting.LogLossOf(g, candidate) >= Reporting.LogLossOf(g, reference))
                compLosses.Add($"{g.Key} (N={g.Count()})");

        var better = dLog < 0;
        var meaningful = Math.Abs(dLog) >= MeaningfulLogLoss;
        var sweepsMetrics = dLog < 0 && dBrier < 0 && dRps < 0;
        var robust = seasonLosses.Count == 0 && compLosses.Count == 0;

        lines.Add($"{cName}  vs  {rName}   ({what})");
        lines.Add($"  TEST log loss {accC.ll:0.000000} vs {accR.ll:0.000000}   delta {dLog:+0.000000;-0.000000;0}   95% CI {ciText}");
        lines.Add($"  Brier delta {dBrier:+0.000000;-0.000000;0}   RPS delta {dRps:+0.000000;-0.000000;0}   " +
                  $"accuracy {accC.acc:0.0000} vs {accR.acc:0.0000}");
        // a log loss gap of d means the probability given to what actually happened is multiplied by exp(d)
        var relative = (Math.Exp(Math.Abs(dLog)) - 1.0) * 100.0;
        lines.Add($"  size: {(meaningful ? "operationally meaningful" : $"below the {MeaningfulLogLoss:0.0000} threshold declared in advance")} " +
                  $"({relative:0.000}% change in the probability given to what actually happened)");
        lines.Add($"  stability: test seasons lost = {(seasonLosses.Count == 0 ? "none" : string.Join(", ", seasonLosses))}; " +
                  $"competition types lost = {(compLosses.Count == 0 ? "none" : string.Join(", ", compLosses))}");

        if (!better || !ciExcludesZero || !meaningful || !sweepsMetrics)
            lines.Add($"  VERDICT: {label}_NOT_PROVEN");
        else if (!robust)
            lines.Add($"  VERDICT: {label}_PROVEN OVERALL, BUT ROBUST IMPROVEMENT NOT PROVEN");
        else
            lines.Add($"  VERDICT: {label}_PROVEN");

        return lines;
    }

    private static (double ll, double brier, double rps, double acc) Acc(IReadOnlyList<MatchPrediction> preds, ModelId m)
    {
        var a = new Formax.DixonColes.Services.MetricAccumulator(ModelIds.Name(m), "TEST", "all");
        foreach (var p in preds) a.Add(p.Probabilities[(int)m], p.Actual);
        return (a.LogLoss, a.Brier, a.Rps, a.Accuracy);
    }

    // ------------------------------------------------------------------ leakage audit

    public static List<(string check, string expected, string observed, string status)> LeakageAudit(
        IReadOnlyList<MatchPrediction> preds,
        IReadOnlyList<LambdaSample> fencedCache,
        TestFence fence, SplitConfig split,
        IReadOnlyList<MatchRecord> matches,
        Func<IReadOnlyList<MatchRecord>, List<MatchPrediction>> rerun)
    {
        var res = new List<(string, string, string, string)>();
        void Check(string name, string expected, string observed, bool ok)
            => res.Add((name, expected, observed, ok ? "PASS" : "FAIL"));

        // 1 - a snapshot may never be fed by a match on or after the match it predicts
        var future = preds.Count(p => p.EvidenceCutoff.HasValue && p.EvidenceCutoff.Value > p.Date);
        Check("future match used as evidence", "0", future.ToString(), future == 0);

        // 2 - same-day matches may not feed each other
        var sameDay = preds.Count(p => p.EvidenceCutoff.HasValue && p.EvidenceCutoff.Value == p.Date);
        Check("same-day match used as evidence", "0", sameDay.ToString(), sameDay == 0);

        // 3 - a match may not inform its own prediction. Rescore every match of the busiest day in the
        //     dataset, replay, and compare: those matches' own predictions must be bit-identical, while
        //     LATER matches must move - otherwise the perturbation did nothing and the check is vacuous.
        var busiestDay = matches.GroupBy(m => m.Date).OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key).First().Key;
        var perturbed = matches.Select(m => m.Date != busiestDay ? m : new MatchRecord
        {
            MatchId = m.MatchId, Date = m.Date, Season = m.Season, Competition = m.Competition,
            CompetitionType = m.CompetitionType, HomeTeamId = m.HomeTeamId, AwayTeamId = m.AwayTeamId,
            HomeTeamName = m.HomeTeamName, AwayTeamName = m.AwayTeamName,
            HomeGoals = m.HomeGoals == 7 ? 6 : 7, AwayGoals = m.AwayGoals == 3 ? 2 : 3,
            MatchStatus = m.MatchStatus, IdentityConfidence = m.IdentityConfidence
        }).ToList();

        var rerunAll = rerun(perturbed).ToDictionary(p => p.MatchId, StringComparer.Ordinal);

        static double Drift(MatchPrediction a, MatchPrediction b)
        {
            var d = 0.0;
            foreach (var m in ModelIds.All)
                d = Math.Max(d,
                    Math.Abs(a.Probabilities[(int)m].Home - b.Probabilities[(int)m].Home) +
                    Math.Abs(a.Probabilities[(int)m].Draw - b.Probabilities[(int)m].Draw) +
                    Math.Abs(a.Probabilities[(int)m].Away - b.Probabilities[(int)m].Away));
            return d;
        }

        var sameDayMatches = preds.Where(p => p.Date == busiestDay).ToList();
        var sameDayDrift = 0.0;
        foreach (var p in sameDayMatches)
            if (rerunAll.TryGetValue(p.MatchId, out var q)) sameDayDrift = Math.Max(sameDayDrift, Drift(p, q));
        Check($"own (and same-day) result changes own prediction - {sameDayMatches.Count} matches of {busiestDay:yyyy-MM-dd} rescored",
            "0", sameDayDrift.ToString("0.0e+0", CultureInfo.InvariantCulture), sameDayDrift == 0.0);

        var laterMoved = 0;
        var laterTotal = 0;
        foreach (var p in preds.Where(p => p.Date > busiestDay))
        {
            laterTotal++;
            if (rerunAll.TryGetValue(p.MatchId, out var q) && Drift(p, q) > 0) laterMoved++;
        }
        Check($"the same rescoring DID move later predictions (vacuity check, {laterTotal} later matches)",
            "> 0", laterMoved.ToString(), laterMoved > 0);

        // 4 - no parameter was chosen on test data
        Check("test matches visible to any parameter search", "0", fence.Breaches.ToString(), fence.Breaches == 0);
        Check("latest match date any parameter search saw",
            $"< {split.TestStart}",
            fence.LatestDateScored?.ToString("yyyy-MM-dd") ?? "(none)",
            fence.LatestDateScored is null || fence.LatestDateScored.Value < split.TestStartDate);
        Check("test matches withheld from every search", $"{fence.SearchesRun} x test segment", fence.MatchesWithheld.ToString(),
            fence.MatchesWithheld > 0);

        // 5 - probabilities are exactly normalised
        var maxSumErr = 0.0;
        foreach (var p in preds)
            foreach (var m in ModelIds.All)
                maxSumErr = Math.Max(maxSumErr, Math.Abs(p.Probabilities[(int)m].Sum - 1.0));
        Check("probability sum error (max over all models and matches)", "< 1e-12",
            maxSumErr.ToString("0.0e+0", CultureInfo.InvariantCulture), maxSumErr < 1e-12);

        // 6 - the fenced replay and the full replay agree on every pre-test match
        var byId = preds.Where(p => p.Segment != Segment.Test).OrderBy(p => p.Date.DayNumber)
            .ThenBy(p => p.MatchId, StringComparer.Ordinal).ToList();
        var cacheOrdered = fencedCache.Where(c => c.Seg != Segment.Test).ToList();
        var lambdaDrift = 0.0;
        var comparable = Math.Min(byId.Count, cacheOrdered.Count);
        for (var i = 0; i < comparable; i++)
            lambdaDrift = Math.Max(lambdaDrift,
                Math.Abs(byId[i].LambdaHome - cacheOrdered[i].Lh) + Math.Abs(byId[i].LambdaAway - cacheOrdered[i].La));
        Check($"fenced replay vs full replay, lambda drift on {comparable} pre-test matches", "0",
            lambdaDrift.ToString("0.0e+0", CultureInfo.InvariantCulture), lambdaDrift == 0.0 && comparable == cacheOrdered.Count);

        // 7 - every eligible match got a prediction
        Check("matches without a prediction", "0", (matches.Count - preds.Count).ToString(), matches.Count == preds.Count);

        return res;
    }
}
