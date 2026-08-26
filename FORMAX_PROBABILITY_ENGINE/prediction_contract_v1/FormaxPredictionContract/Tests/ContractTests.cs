using Formax.Contract.Models;
using Formax.Contract.Services;
using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.ModelValidation.Services;
using Formax.Prediction.Config;
using Formax.Prediction.Services;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;
using Formax.TeamStrength.Services;
using Formax.TeamStrength.Tests;

namespace Formax.Contract.Tests;

public static class ContractTests
{
    private static MatchPrediction Raw(string id, double h, double d, double a,
        DateOnly? date = null, DateOnly? cutoff = null)
    {
        var probs = new ProbTriple[ModelIds.All.Length];
        probs[(int)ModelId.IndependentPoisson] = new ProbTriple(h, d, a, 1e-6);
        return new MatchPrediction
        {
            MatchId = id,
            Date = date ?? DateOnly.Parse("2024-03-01"),
            Segment = Segment.Test,
            Season = "2023/24",
            Competition = "Premier League",
            CompetitionType = "DOMESTIC_LEAGUE",
            HomeTeam = "H", AwayTeam = "A",
            LambdaHome = 1.6, LambdaAway = 1.1, Lambda3 = 0,
            Probabilities = probs,
            Actual = Outcome.HomeWin,
            HomeGoals = 2, AwayGoals = 0,
            HomeColdStartClass = "Rich", AwayColdStartClass = "Rich",
            HomePriorWeight = 0, AwayPriorWeight = 0,
            HomePriorSource = "GLOBAL_NEUTRAL", AwayPriorSource = "GLOBAL_NEUTRAL",
            IsColdStart = false,
            EvidenceCutoff = cutoff ?? DateOnly.Parse("2024-02-24")
        };
    }

    private static TeamStrengthSnapshot Snap(string matchId, string side, int matches) => new()
    {
        MatchId = matchId, TeamId = side, TeamName = side,
        MatchDate = DateOnly.Parse("2024-03-01"), Side = side,
        CompetitionType = "DOMESTIC_LEAGUE", Competition = "Premier League",
        AttackStrength = 1.2, DefenseStrength = 0.9, OverallStrength = 1.33,
        MatchesUsed = matches, EffectiveMatches = matches,
        HomeMatchesUsed = matches / 2, AwayMatchesUsed = matches / 2,
        Confidence = ConfidenceLevel.High,
        ColdStartClass = matches == 0 ? ColdStartClass.NoHistory
                       : matches < 3 ? ColdStartClass.Limited
                       : matches < 10 ? ColdStartClass.Established : ColdStartClass.Rich,
        PriorSource = "GLOBAL_NEUTRAL", PriorWeight = matches == 0 ? 1.0 : 0.0,
        LastMatchDate = DateOnly.Parse("2024-02-24")
    };

    public static void Register(TestRunner r, string? datasetPath, string? splitPath,
        string? dcConfigPath, string? validatedTsPath, string? gateConfigPath)
    {
        if (validatedTsPath is null) return;
        var gateCfg = gateConfigPath is not null ? GateConfig.Load(gateConfigPath) : new GateConfig();
        var versions = ContractVersions.Current;
        ContractService Service() => new(gateCfg, versions, validatedTsPath);

        // ---------------------------------------------------------------- accepted / rejected

        r.Add("an accepted prediction carries the model probabilities unchanged", () =>
        {
            var raw = Raw("m1", 0.6452, 0.2141, 0.1407);
            var c = Service().Build(raw, Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);

            TestRunner.Equal(GateStatus.Accepted, c.GateStatus, "gate status");
            TestRunner.True(c.PredictionEligible, $"refused: {c.GateReason}");
            var p = raw.Probabilities[(int)ModelId.IndependentPoisson];
            TestRunner.Equal(p.Home, c.HomeProbability!.Value, 0.0, "home probability was altered");
            TestRunner.Equal(p.Draw, c.DrawProbability!.Value, 0.0, "draw probability was altered");
            TestRunner.Equal(p.Away, c.AwayProbability!.Value, 0.0, "away probability was altered");
            TestRunner.Equal("OK", c.GateReason, "reason of an accepted prediction");
        });

        r.Add("a rejected prediction carries no probability at all", () =>
        {
            var c = Service().Build(Raw("m1", 0.5, 0.25, 0.25),
                Snap("m1", "HOME", 40), Snap("m1", "AWAY", 0), "CONFIRMED", "CONFIRMED", 5000);

            TestRunner.Equal(GateStatus.Rejected, c.GateStatus, "gate status");
            TestRunner.True(!c.PredictionEligible, "a zero-history side was published");
            TestRunner.True(c.HomeProbability is null && c.DrawProbability is null && c.AwayProbability is null,
                "a rejected prediction still carried probabilities");
            TestRunner.True(c.GateReason.Contains("NO_TEAM_HISTORY"), $"gate reason: {c.GateReason}");
        });

        r.Add("probabilities are a distribution inside [0,1]", () =>
        {
            foreach (var (h, d, a) in new[] { (0.9745, 0.0180, 0.0075), (0.3333, 0.3334, 0.3333), (0.05, 0.15, 0.80) })
            {
                var c = Service().Build(Raw("m1", h, d, a),
                    Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
                TestRunner.Equal(1.0, c.ProbabilitySum, 1e-12, "probability sum");
                foreach (var p in new[] { c.HomeProbability!.Value, c.DrawProbability!.Value, c.AwayProbability!.Value })
                    TestRunner.True(p >= 0 && p <= 1, $"probability {p} outside [0,1]");
            }
        });

        r.Add("the contract exposes decimals, never a formatted string", () =>
        {
            var c = Service().Build(Raw("m1", 0.6452, 0.2141, 0.1407),
                Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
            // the property type is double? - a percent string could not be assigned to it - and the
            // value keeps full precision rather than the two decimals a display would use
            TestRunner.True(Math.Abs(c.HomeProbability!.Value - Math.Round(c.HomeProbability.Value, 2)) > 0,
                "the probability arrived already rounded to two decimals");
        });

        // ---------------------------------------------------------------- 7. versioning

        r.Add("every prediction is stamped with all four versions", () =>
        {
            var c = Service().Build(Raw("m1", 0.5, 0.25, 0.25),
                Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
            TestRunner.Equal("INDEPENDENT_POISSON_V2", c.Versions.ModelVersion, "model version");
            TestRunner.Equal("TEAM_STRENGTH_V2", c.Versions.TeamStrengthVersion, "team strength version");
            TestRunner.Equal("GATE_V1", c.Versions.GateVersion, "gate version");
            TestRunner.Equal("NONE", c.Versions.CalibrationVersion, "calibration version");
        });

        // ---------------------------------------------------------------- 8. evidence cutoff

        r.Add("evidence cutoff is strictly before the match and never after the timestamp", () =>
        {
            var c = Service().Build(Raw("m1", 0.5, 0.25, 0.25),
                Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
            TestRunner.True(c.EvidenceCutoff!.Value < c.MatchDate, "evidence is not strictly pre-match");
            TestRunner.True(c.EvidenceCutoff.Value <= DateOnly.FromDateTime(c.PredictionTimestamp),
                "evidence is newer than the prediction that used it");
        });

        r.Add("evidence dated on the match day is refused", () =>
        {
            var c = Service().Build(
                Raw("m1", 0.5, 0.25, 0.25, DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-03-01")),
                Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
            TestRunner.Equal(GateStatus.Rejected, c.GateStatus, "same-day evidence was accepted");
            TestRunner.True(c.GateReason.Contains("EVIDENCE_NOT_STRICTLY_PRE_MATCH"), $"reason: {c.GateReason}");
        });

        // ---------------------------------------------------------------- 4. confidence vs probability

        r.Add("confidence is evidence, not the probability", () =>
        {
            var svc = Service();
            var home = Snap("m1", "HOME", 40);
            var away = Snap("m1", "AWAY", 35);

            var sure = svc.Build(Raw("m1", 0.93, 0.04, 0.03), home, away, "CONFIRMED", "CONFIRMED", 5000);
            var unsure = svc.Build(Raw("m1", 0.34, 0.33, 0.33), home, away, "CONFIRMED", "CONFIRMED", 5000);
            TestRunner.Equal(sure.ConfidenceClass, unsure.ConfidenceClass,
                "the same evidence produced different confidence for different probabilities");

            // and the contract's own example: a high probability with LOW confidence
            var thin = svc.Build(Raw("m1", 0.78, 0.14, 0.08), home, Snap("m1", "AWAY", 2),
                "CONFIRMED", "CONFIRMED", 5000);
            TestRunner.True(thin.PredictionEligible, $"refused: {thin.GateReason}");
            TestRunner.Equal(ConfidenceClass.Low, thin.ConfidenceClass, "78% on two matches of evidence");
            TestRunner.Equal(0.78, thin.HomeProbability!.Value, 1e-9, "and the probability is untouched");
        });

        // ---------------------------------------------------------------- 10. immutability

        r.Add("the same inputs produce the same prediction id", () =>
        {
            var svc = Service();
            var a = svc.Build(Raw("m1", 0.5, 0.25, 0.25), Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
            var b = svc.Build(Raw("m1", 0.5, 0.25, 0.25), Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
            TestRunner.Equal(a.PredictionId, b.PredictionId, "the id is not deterministic");
            TestRunner.Equal(a.ContentHash, b.ContentHash, "the content hash is not deterministic");
        });

        r.Add("newer evidence produces a NEW prediction id, not an edit", () =>
        {
            var svc = Service();
            var older = svc.Build(Raw("m1", 0.5, 0.25, 0.25, cutoff: DateOnly.Parse("2024-02-24")),
                Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
            var newer = svc.Build(Raw("m1", 0.61, 0.22, 0.17, cutoff: DateOnly.Parse("2024-02-28")),
                Snap("m1", "HOME", 41), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);

            TestRunner.True(older.PredictionId != newer.PredictionId,
                "a prediction from newer evidence reused the old id - that would be an edit");

            var store = new PredictionStore();
            TestRunner.Equal(PublishOutcome.Published, store.Publish(older), "first publish");
            TestRunner.Equal(PublishOutcome.Published, store.Publish(newer), "second publish");
            TestRunner.Equal(2, store.History("m1").Count, "both must be kept");
            TestRunner.Equal(newer.PredictionId, store.Current("m1")!.Prediction.PredictionId, "current is the newest");
            TestRunner.Equal(0.5, store.History("m1")[0].Prediction.HomeProbability!.Value, 1e-9,
                "the older prediction must still say what it said");
        });

        r.Add("a published prediction cannot be rewritten", () =>
        {
            var svc = Service();
            var original = svc.Build(Raw("m1", 0.5, 0.25, 0.25),
                Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
            var store = new PredictionStore();
            TestRunner.Equal(PublishOutcome.Published, store.Publish(original), "first publish");

            var tampered = original with { HomeProbability = 0.90, DrawProbability = 0.05, AwayProbability = 0.05 };
            TestRunner.Equal(PublishOutcome.RejectedImmutable, store.Publish(tampered), "a rewrite must be refused");
            TestRunner.Equal(0.5, store.Get(original.PredictionId)!.Prediction.HomeProbability!.Value, 1e-12,
                "the stored value changed despite the refusal");
            TestRunner.Equal(1, store.ImmutabilityViolationsRefused, "the refusal must be counted");
        });

        r.Add("republishing an identical prediction is a harmless no-op", () =>
        {
            var svc = Service();
            var c = svc.Build(Raw("m1", 0.5, 0.25, 0.25), Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
            var store = new PredictionStore();
            store.Publish(c);
            TestRunner.Equal(PublishOutcome.AlreadyPublished, store.Publish(c), "republish");
            TestRunner.Equal(1, store.Count, "republishing must not duplicate the row");
            TestRunner.Equal(1, store.IdempotentRepublishes, "the no-op must be counted");
        });

        // ---------------------------------------------------------------- 11. settlement

        r.Add("settlement attaches a result without touching the prediction", () =>
        {
            var svc = Service();
            var c = svc.Build(Raw("m1", 0.55, 0.25, 0.20), Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
            var store = new PredictionStore();
            store.Publish(c);
            var before = store.Get(c.PredictionId)!.Prediction.ContentHash;

            var results = new Dictionary<string, (int, int, DateOnly)> { ["m1"] = (2, 0, DateOnly.Parse("2024-03-01")) };
            var report = store.Settle(results, new DateTime(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc));

            var row = store.Get(c.PredictionId)!;
            TestRunner.Equal(1, report.Settled, "settled count");
            TestRunner.Equal(before, row.Prediction.ContentHash, "settlement changed the prediction");
            TestRunner.True(row.IsIntact, "the row is no longer what was published");
            TestRunner.Equal(Outcome.HomeWin, row.Settlement!.ActualResult, "actual result");
            TestRunner.Equal(-Math.Log(0.55), row.LogLoss!.Value, 1e-12, "settled log loss");
        });

        r.Add("a result may not be attached twice, nor before the prediction", () =>
        {
            var svc = Service();
            var c = svc.Build(Raw("m1", 0.55, 0.25, 0.20), Snap("m1", "HOME", 40), Snap("m1", "AWAY", 35), "CONFIRMED", "CONFIRMED", 5000);
            var store = new PredictionStore();
            store.Publish(c);
            var when = new DateTime(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc);

            store.Settle(new Dictionary<string, (int, int, DateOnly)> { ["m1"] = (2, 0, DateOnly.Parse("2024-03-01")) }, when);
            var again = store.Settle(new Dictionary<string, (int, int, DateOnly)> { ["m1"] = (0, 3, DateOnly.Parse("2024-03-01")) }, when);
            TestRunner.Equal(0, again.Settled, "a settled row must not settle twice");
            TestRunner.Equal(1, again.AlreadySettled, "and must say so");
            TestRunner.Equal(Outcome.HomeWin, store.Get(c.PredictionId)!.Settlement!.ActualResult,
                "the first result must stand");

            var store2 = new PredictionStore();
            store2.Publish(c);
            var early = store2.Settle(new Dictionary<string, (int, int, DateOnly)> { ["m1"] = (2, 0, DateOnly.Parse("2024-01-01")) }, when);
            TestRunner.Equal(1, early.RejectedAsEarly, "a result older than the prediction must be refused");
            TestRunner.True(store2.Get(c.PredictionId)!.Settlement is null, "and must not be attached");
        });

        r.Add("a rejected prediction is logged but never scored", () =>
        {
            var svc = Service();
            var c = svc.Build(Raw("m1", 0.55, 0.25, 0.20), Snap("m1", "HOME", 40), Snap("m1", "AWAY", 0), "CONFIRMED", "CONFIRMED", 5000);
            var store = new PredictionStore();
            store.Publish(c);
            store.Settle(new Dictionary<string, (int, int, DateOnly)> { ["m1"] = (2, 0, DateOnly.Parse("2024-03-01")) },
                new DateTime(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc));

            var row = store.Get(c.PredictionId)!;
            TestRunner.True(row.Settlement is not null, "a rejected prediction should still be settleable for the record");
            TestRunner.True(row.LogLoss is null, "a rejected prediction must never produce a score");
        });

        // ---------------------------------------------------------------- 16. real data regression

        if (datasetPath is null || dcConfigPath is null) return;

        r.Add("real data: the contract reproduces the model_validation_v2 probabilities exactly", () =>
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
            var svc = Service();

            var store = new PredictionStore();
            var accepted = 0;
            var drift = 0.0;
            foreach (var raw in preds)
            {
                snaps.TryGetValue((raw.MatchId, "HOME"), out var h);
                snaps.TryGetValue((raw.MatchId, "AWAY"), out var a);
                var c = svc.Build(raw, h, a, identity[raw.MatchId], identity[raw.MatchId], cov.For(raw.MatchId));
                store.Publish(c);
                if (!c.PredictionEligible) continue;
                accepted++;
                var p = raw.Probabilities[(int)ModelId.IndependentPoisson];
                drift = Math.Max(drift, Math.Abs(c.HomeProbability!.Value - p.Home));
                drift = Math.Max(drift, Math.Abs(c.DrawProbability!.Value - p.Draw));
                drift = Math.Max(drift, Math.Abs(c.AwayProbability!.Value - p.Away));
            }

            TestRunner.Equal(33901, preds.Count, "prediction count");
            TestRunner.Equal(33203, accepted, "accepted count");
            TestRunner.Equal(0.0, drift, 0.0, "the contract altered a model probability");
            TestRunner.Equal(33901, store.Count, "every prediction must be stored exactly once");

            double Ll(Segment seg)
            {
                var sum = 0.0; var n = 0;
                foreach (var p in preds.Where(x => x.Segment == seg))
                {
                    sum += -Math.Log(Math.Max(p.Probabilities[(int)ModelId.IndependentPoisson][p.Actual], 1e-15));
                    n++;
                }
                return sum / n;
            }
            TestRunner.Equal(0.987473, Ll(Segment.Validation), 5e-6, "validation log loss");
            TestRunner.Equal(0.993544, Ll(Segment.Test), 5e-6, "test log loss");
        });
    }
}
