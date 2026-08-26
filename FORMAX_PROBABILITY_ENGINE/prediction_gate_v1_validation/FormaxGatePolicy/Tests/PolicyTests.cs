using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.GatePolicy.Models;
using Formax.GatePolicy.Services;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.ModelValidation.Services;
using Formax.Prediction.Config;
using Formax.Prediction.Models;
using Formax.Prediction.Services;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;
using Formax.TeamStrength.Services;
using Formax.TeamStrength.Tests;

namespace Formax.GatePolicy.Tests;

public static class PolicyTests
{
    private static MatchPrediction Raw(string id, double h, double d, double a, string cls = "Rich")
    {
        var probs = new ProbTriple[ModelIds.All.Length];
        var p = new ProbTriple(h, d, a, 1e-6);
        probs[(int)ModelId.IndependentPoisson] = p;
        probs[(int)ModelId.TeamStrength] = p;
        probs[(int)ModelId.Simple] = p;
        return new MatchPrediction
        {
            MatchId = id,
            Date = DateOnly.Parse("2024-03-01"),
            Segment = Segment.Validation,
            Season = "2023/24",
            Competition = "Premier League",
            CompetitionType = "DOMESTIC_LEAGUE",
            HomeTeam = "H", AwayTeam = "A",
            LambdaHome = 1.6, LambdaAway = 1.1, Lambda3 = 0,
            Probabilities = probs,
            Actual = Outcome.HomeWin,
            HomeGoals = 2, AwayGoals = 0,
            HomeColdStartClass = cls, AwayColdStartClass = cls,
            HomePriorWeight = 0, AwayPriorWeight = 0,
            HomePriorSource = "GLOBAL_NEUTRAL", AwayPriorSource = "GLOBAL_NEUTRAL",
            IsColdStart = cls == "NoHistory",
            EvidenceCutoff = DateOnly.Parse("2024-02-24")
        };
    }

    private static TeamStrengthSnapshot Snap(string matchId, string side, int matches, double effective) => new()
    {
        MatchId = matchId, TeamId = side, TeamName = side,
        MatchDate = DateOnly.Parse("2024-03-01"), Side = side,
        CompetitionType = "DOMESTIC_LEAGUE", Competition = "Premier League",
        AttackStrength = 1.2, DefenseStrength = 0.9, OverallStrength = 1.33,
        MatchesUsed = matches, EffectiveMatches = effective,
        HomeMatchesUsed = matches / 2, AwayMatchesUsed = matches / 2,
        Confidence = ConfidenceLevel.High,
        ColdStartClass = matches == 0 ? ColdStartClass.NoHistory
                       : matches < 3 ? ColdStartClass.Limited
                       : matches < 5 ? ColdStartClass.Developing
                       : matches < 10 ? ColdStartClass.Established : ColdStartClass.Rich,
        PriorSource = "GLOBAL_NEUTRAL", PriorWeight = matches == 0 ? 1.0 : 0.0,
        LastMatchDate = DateOnly.Parse("2024-02-24")
    };

    private static MatchContext Ctx(string id, int homeMatches, int awayMatches, double homeEff, double awayEff,
        int competitionObserved = 5000, double h = 0.5, double d = 0.25, double a = 0.25)
    {
        var weakest = Math.Min(homeMatches, awayMatches);
        var cls = weakest == 0 ? "NoHistory" : weakest < 3 ? "Limited" : weakest < 5 ? "Developing"
                : weakest < 10 ? "Established" : "Rich";
        var raw = Raw(id, h, d, a, cls);
        var p = raw.Probabilities[(int)ModelId.IndependentPoisson];
        return new MatchContext
        {
            Raw = raw,
            Home = Snap(id, "HOME", homeMatches, homeEff),
            Away = Snap(id, "AWAY", awayMatches, awayEff),
            IdentityConfidence = "CONFIRMED",
            CompetitionMatchesObserved = competitionObserved,
            Segment = Segment.Validation,
            WeakestColdStartClass = cls,
            ModelLogLoss = -Math.Log(Math.Max(p[raw.Actual], 1e-15))
        };
    }

    public static void Register(TestRunner r, string? datasetPath, string? splitPath,
        string? dcConfigPath, string? validatedTsPath, string? gateConfigPath)
    {
        var baseline = gateConfigPath is not null ? GateConfig.Load(gateConfigPath) : new GateConfig();

        // ---------------------------------------------------------------- the policy must actually be a policy

        r.Add("policy 0 publishes even a side with no history at all", () =>
        {
            var ctx = new List<MatchContext> { Ctx("m1", 0, 40, 0, 40) };
            var published = PolicyEvaluator.Decide(ctx, PolicyGrid.NoGate, baseline);
            TestRunner.True(published[0], "the null policy refused a zero-history match - it cannot express itself");
        });

        r.Add("policy 1 refuses a side with no history at all", () =>
        {
            var ctx = new List<MatchContext> { Ctx("m1", 0, 40, 0, 40) };
            var published = PolicyEvaluator.Decide(ctx, PolicyGrid.Current, baseline);
            TestRunner.True(!published[0], "the current policy published a zero-history match");
        });

        r.Add("a stricter history bar can only lower coverage", () =>
        {
            var ctx = new List<MatchContext>();
            for (var i = 0; i < 40; i++) ctx.Add(Ctx($"m{i}", i % 12, 40, Math.Max(0, i % 12 - 0.02), 40));

            var previous = int.MaxValue;
            foreach (var bar in PolicyGrid.MinPriorMatches)
            {
                var published = PolicyEvaluator.Decide(ctx, new Policy(bar, 0), baseline).Count(x => x);
                TestRunner.True(published <= previous,
                    $"raising the bar to {bar} increased coverage from {previous} to {published}");
                previous = published;
            }
        });

        r.Add("a stricter competition bar can only lower coverage", () =>
        {
            var ctx = new List<MatchContext>();
            for (var i = 0; i < 30; i++) ctx.Add(Ctx($"m{i}", 40, 40, 40, 40, competitionObserved: i * 20));

            var previous = int.MaxValue;
            foreach (var bar in PolicyGrid.MinCompetitionMatches)
            {
                var published = PolicyEvaluator.Decide(ctx, new Policy(1, bar), baseline).Count(x => x);
                TestRunner.True(published <= previous,
                    $"raising the competition bar to {bar} increased coverage from {previous} to {published}");
                previous = published;
            }
        });

        // ---------------------------------------------------------------- 9. the decay artefact must not return

        r.Add("regression: one prior match with a decayed count just under 1 still passes policy 1", () =>
        {
            // this is the exact shape that produced 533 spurious INSUFFICIENT_HISTORY refusals before
            var ctx = new List<MatchContext> { Ctx("m1", 1, 40, 0.978, 40) };
            var published = PolicyEvaluator.Decide(ctx, PolicyGrid.Current, baseline);
            TestRunner.True(published[0],
                "a team with exactly one prior match was refused again - the decay artefact is back");

            // and it must be refused once the bar genuinely asks for two
            var stricter = PolicyEvaluator.Decide(ctx, new Policy(2, 50), baseline);
            TestRunner.True(!stricter[0], "policy 2 should refuse a one-match side");
        });

        r.Add("regression: the staleness net travels with the history bar", () =>
        {
            TestRunner.Equal(0.0, new Policy(0, 0).MinEffectiveMatches, 0.0, "policy 0 must not keep a staleness floor");
            TestRunner.Equal(0.5, new Policy(1, 50).MinEffectiveMatches, 0.0, "policy 1 staleness floor");
            TestRunner.True(new Policy(1, 50).MinEffectiveMatches < 1.0,
                "the staleness floor must stay below the integer history bar");
        });

        // ---------------------------------------------------------------- 3. the gate may not touch the model

        r.Add("every policy sees byte-identical model probabilities", () =>
        {
            var ctx = new List<MatchContext>();
            for (var i = 0; i < 30; i++) ctx.Add(Ctx($"m{i}", i % 12, 40, Math.Max(0, i % 12 - 0.02), 40));

            var reference = ctx.Select(c => c.Raw.Probabilities[(int)ModelId.IndependentPoisson]).ToList();
            var allLosses = new List<double>();

            foreach (var policy in PolicyGrid.All())
            {
                var published = PolicyEvaluator.Decide(ctx, policy, baseline);
                var res = PolicyEvaluator.Measure(ctx, published, policy, "VALIDATION", "OVERALL", "all", _ => true);
                allLosses.Add(res.AllLogLoss);

                for (var i = 0; i < ctx.Count; i++)
                {
                    var p = ctx[i].Raw.Probabilities[(int)ModelId.IndependentPoisson];
                    TestRunner.Equal(reference[i].Home, p.Home, 0.0, $"policy {policy.Name} altered a probability");
                }
            }
            TestRunner.Equal(1, allLosses.Distinct().Count(),
                "the log loss over ALL matches must be identical for every policy");
        });

        r.Add("the same raw probability is published under one policy and refused under another", () =>
        {
            var ctx = new List<MatchContext> { Ctx("m1", 2, 40, 1.98, 40, h: 0.71, d: 0.19, a: 0.10) };
            var loose = PolicyEvaluator.Decide(ctx, new Policy(1, 50), baseline);
            var strict = PolicyEvaluator.Decide(ctx, new Policy(5, 50), baseline);

            TestRunner.True(loose[0], "the loose policy should publish");
            TestRunner.True(!strict[0], "the strict policy should refuse");
            TestRunner.Equal(0.71, ctx[0].Raw.Probabilities[(int)ModelId.IndependentPoisson].Home, 1e-9,
                "and the probability itself is the same number in both cases");
        });

        r.Add("policy application changes only the two swept thresholds", () =>
        {
            var applied = new Policy(7, 123).Apply(baseline);
            TestRunner.Equal(7, applied.MinPriorMatchesPerTeam, "history bar");
            TestRunner.Equal(123, applied.MinCompetitionMatchesObserved, "competition bar");
            TestRunner.Equal(baseline.RequiredIdentityConfidence, applied.RequiredIdentityConfidence, "identity");
            TestRunner.Equal(baseline.LambdaMin, applied.LambdaMin, 0.0, "lambda min");
            TestRunner.Equal(baseline.LambdaMax, applied.LambdaMax, 0.0, "lambda max");
            TestRunner.Equal(baseline.ProbabilitySumTolerance, applied.ProbabilitySumTolerance, 0.0, "sum tolerance");
            TestRunner.Equal(baseline.ConfidenceHighMin, applied.ConfidenceHighMin, "confidence high");
            TestRunner.Equal(baseline.ConfidenceLowMin, applied.ConfidenceLowMin, "confidence low");
            TestRunner.Equal(baseline.MaxPriorWeightForFullConfidence, applied.MaxPriorWeightForFullConfidence, 0.0, "prior weight cap");
        });

        r.Add("confidence does not move when the policy moves", () =>
        {
            var raw = Raw("m1", 0.5, 0.25, 0.25);
            var home = Snap("m1", "HOME", 40, 40);
            var away = Snap("m1", "AWAY", 40, 40);

            var loose = new PredictionService(new Policy(1, 50).Apply(baseline))
                .Build(raw, home, away, "CONFIRMED", "CONFIRMED", 5000);
            var strict = new PredictionService(new Policy(10, 200).Apply(baseline))
                .Build(raw, home, away, "CONFIRMED", "CONFIRMED", 5000);

            TestRunner.Equal(loose.ConfidenceClass, strict.ConfidenceClass,
                "the gate threshold changed the confidence class");
        });

        // ---------------------------------------------------------------- 12. the selective-risk test itself

        r.Add("the selection test spots a gate that refuses the worst matches", () =>
        {
            var losses = Enumerable.Range(0, 400).Select(i => (double)i / 100.0).ToList();
            var published = new bool[400];
            for (var i = 0; i < 400; i++) published[i] = i < 340;      // refuse the 60 highest losses
            var res = SelectionTest.Run(losses, published, "worst-refused", "TEST", draws: 400);
            TestRunner.True(res.PValue < 0.05, $"a perfect gate scored p={res.PValue:0.000}");
            TestRunner.True(res.ActualPublishedLogLoss < res.NullP025, "and must sit below the null interval");
        });

        r.Add("the selection test spots a gate that refuses the best matches", () =>
        {
            var losses = Enumerable.Range(0, 400).Select(i => (double)i / 100.0).ToList();
            var published = new bool[400];
            for (var i = 0; i < 400; i++) published[i] = i >= 60;      // refuse the 60 LOWEST losses
            var res = SelectionTest.Run(losses, published, "best-refused", "TEST", draws: 400);
            TestRunner.True(res.PValue > 0.95, $"a value-destroying gate scored p={res.PValue:0.000}");
            TestRunner.True(res.Verdict.StartsWith("WORSE THAN CHANCE", StringComparison.Ordinal),
                $"verdict was '{res.Verdict}'");
        });

        r.Add("the selection test cannot tell a random gate from chance", () =>
        {
            var losses = Enumerable.Range(0, 400).Select(i => (double)((i * 37) % 400) / 100.0).ToList();
            var published = new bool[400];
            for (var i = 0; i < 400; i++) published[i] = i % 20 != 0;   // refuse an arbitrary, loss-blind 5%
            var res = SelectionTest.Run(losses, published, "random", "TEST", draws: 1000);
            TestRunner.True(res.PValue > 0.05 && res.PValue < 0.95,
                $"a loss-blind gate should look random, scored p={res.PValue:0.000}");
        });

        r.Add("the selection test is deterministic", () =>
        {
            var losses = Enumerable.Range(0, 200).Select(i => (double)((i * 13) % 200) / 100.0).ToList();
            var published = new bool[200];
            for (var i = 0; i < 200; i++) published[i] = i % 7 != 0;
            var a = SelectionTest.Run(losses, published, "x", "y", draws: 300);
            var b = SelectionTest.Run(losses, published, "x", "y", draws: 300);
            TestRunner.Equal(a.PValue, b.PValue, 0.0, "p-value is not reproducible");
            TestRunner.Equal(a.NullMean, b.NullMean, 0.0, "null mean is not reproducible");
        });

        // ---------------------------------------------------------------- real data

        if (datasetPath is null || validatedTsPath is null || dcConfigPath is null) return;

        r.Add("real data: the current policy reproduces the shipped Prediction Gate V1 counts", () =>
        {
            var ts = TeamStrengthConfig.Load(validatedTsPath);
            var dc = DixonColesConfig.Load(dcConfigPath);
            var split = splitPath is not null ? SplitConfig.Load(splitPath) : SplitConfig.Load("(none)");
            var matches = MatchCsvReader.Read(datasetPath, ts).Matches;

            var built = new TeamStrengthService(ts).Build(matches);
            var snaps = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side));
            var rows = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side), StrengthPipeline.ToRow);

            var preds = new List<MatchPrediction>();
            new BacktestV2(dc, new ModelParameters { Rho = 0.0, BivariateC = 0.0 }, split)
                .Run(matches, rows, ModelMask.IndependentPoisson, preds.Add);

            var cov = CompetitionCoverage.Build(matches);
            var identity = matches.ToDictionary(m => m.MatchId, m => m.IdentityConfidence, StringComparer.Ordinal);
            var svc = new PredictionService(PolicyGrid.Current.Apply(baseline));

            var published = 0;
            foreach (var raw in preds)
            {
                snaps.TryGetValue((raw.MatchId, "HOME"), out var h);
                snaps.TryGetValue((raw.MatchId, "AWAY"), out var a);
                var dto = svc.Build(raw, h, a, identity[raw.MatchId], identity[raw.MatchId], cov.For(raw.MatchId));
                if (dto.PredictionEligible) published++;
            }

            TestRunner.Equal(33901, preds.Count, "prediction count");
            TestRunner.Equal(33203, published, "published count under the shipped policy");
        });
    }
}
