using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
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

namespace Formax.Prediction.Tests;

public static class GateTests
{
    private static GateConfig Cfg() => new();

    /// <summary>A gate input that passes everything. Individual tests break exactly one thing.</summary>
    private static GateInput Good() => new()
    {
        HomeSnapshotPresent = true,
        AwaySnapshotPresent = true,
        HomeIdentityConfidence = "CONFIRMED",
        AwayIdentityConfidence = "CONFIRMED",
        HomePriorMatches = 40,
        AwayPriorMatches = 35,
        HomeEffectiveMatches = 40,
        AwayEffectiveMatches = 35,
        HomePriorWeight = 0.0,
        AwayPriorWeight = 0.0,
        CompetitionMatchesObserved = 5000,
        LambdaHome = 1.6,
        LambdaAway = 1.1,
        ProbHome = 0.5,
        ProbDraw = 0.25,
        ProbAway = 0.25,
        MatchDate = DateOnly.Parse("2024-03-01"),
        EvidenceCutoff = DateOnly.Parse("2024-02-24")
    };

    private static MatchPrediction Raw(ProbTriple p, DateOnly date, DateOnly? cutoff,
        double lh = 1.6, double la = 1.1)
    {
        var probs = new ProbTriple[ModelIds.All.Length];
        probs[(int)ModelId.IndependentPoisson] = p;
        return new MatchPrediction
        {
            MatchId = "M1",
            Date = date,
            Segment = Segment.Test,
            Season = "2024/25",
            Competition = "Premier League",
            CompetitionType = "DOMESTIC_LEAGUE",
            HomeTeam = "H",
            AwayTeam = "A",
            LambdaHome = lh,
            LambdaAway = la,
            Lambda3 = 0,
            Probabilities = probs,
            Actual = Outcome.HomeWin,
            HomeGoals = 2,
            AwayGoals = 0,
            HomeColdStartClass = "Rich",
            AwayColdStartClass = "Rich",
            HomePriorWeight = 0,
            AwayPriorWeight = 0,
            HomePriorSource = "GLOBAL_NEUTRAL",
            AwayPriorSource = "GLOBAL_NEUTRAL",
            IsColdStart = false,
            EvidenceCutoff = cutoff
        };
    }

    private static TeamStrengthSnapshot Snap(string side, int matches, double effective, double priorWeight,
        string cls = "Rich") => new()
        {
            MatchId = "M1",
            TeamId = side,
            TeamName = side,
            MatchDate = DateOnly.Parse("2024-03-01"),
            Side = side,
            CompetitionType = "DOMESTIC_LEAGUE",
            Competition = "Premier League",
            AttackStrength = 1.2,
            DefenseStrength = 0.9,
            OverallStrength = 1.33,
            MatchesUsed = matches,
            EffectiveMatches = effective,
            HomeMatchesUsed = matches / 2,
            AwayMatchesUsed = matches / 2,
            Confidence = ConfidenceLevel.High,
            ColdStartClass = Enum.Parse<ColdStartClass>(cls),
            PriorSource = "GLOBAL_NEUTRAL",
            PriorWeight = priorWeight,
            LastMatchDate = DateOnly.Parse("2024-02-24")
        };

    public static void Register(TestRunner r, string? datasetPath, string? splitPath,
        string? dcConfigPath, string? validatedTsPath)
    {
        var gate = new PredictionGate(Cfg());

        // ---------------------------------------------------------------- gate accept

        r.Add("gate accepts a fully evidenced match", () =>
        {
            var res = gate.Evaluate(Good());
            TestRunner.True(res.Eligible, $"a good match was refused: {res.Reason}");
            TestRunner.Equal("OK", res.Reason, "reason of an accepted match");
        });

        // ---------------------------------------------------------------- gate reject, one cause at a time

        r.Add("gate rejects an unconfirmed identity", () =>
        {
            var g = Good() with { AwayIdentityConfidence = "AMBIGUOUS" };
            var res = gate.Evaluate(g);
            TestRunner.True(!res.Eligible, "unconfirmed identity was accepted");
            TestRunner.True(res.Reason.Contains("IDENTITY_UNCONFIRMED"), $"wrong reason: {res.Reason}");
        });

        r.Add("gate rejects a missing snapshot", () =>
        {
            var res = gate.Evaluate(Good() with { HomeSnapshotPresent = false });
            TestRunner.True(!res.Eligible, "missing snapshot was accepted");
            TestRunner.True(res.Reason.Contains("SNAPSHOT_MISSING"), $"wrong reason: {res.Reason}");
        });

        r.Add("gate rejects a side with no history at all", () =>
        {
            var res = gate.Evaluate(Good() with { AwayPriorMatches = 0, AwayEffectiveMatches = 0, AwayPriorWeight = 1.0 });
            TestRunner.True(!res.Eligible, "a debutant was accepted");
            TestRunner.True(res.Reason.Contains("NO_TEAM_HISTORY"), $"wrong reason: {res.Reason}");
        });

        r.Add("no history and insufficient history are different refusals", () =>
        {
            var cfg = Cfg();
            cfg.MinPriorMatchesPerTeam = 5;
            var strict = new PredictionGate(cfg);

            var none = strict.Evaluate(Good() with { AwayPriorMatches = 0, AwayEffectiveMatches = 0 });
            TestRunner.True(none.Reason.Contains("NO_TEAM_HISTORY"), $"zero history: {none.Reason}");
            TestRunner.True(!none.Reason.Contains("INSUFFICIENT_HISTORY"), "zero history must not also report INSUFFICIENT");

            var thin = strict.Evaluate(Good() with { AwayPriorMatches = 2, AwayEffectiveMatches = 2 });
            TestRunner.True(thin.Reason.Contains("INSUFFICIENT_HISTORY"), $"thin history: {thin.Reason}");
        });

        r.Add("gate rejects a competition whose baseline is still a seed", () =>
        {
            var res = gate.Evaluate(Good() with { CompetitionMatchesObserved = 12 });
            TestRunner.True(!res.Eligible, "uncovered competition was accepted");
            TestRunner.True(res.Reason.Contains("COMPETITION_NOT_COVERED"), $"wrong reason: {res.Reason}");
        });

        r.Add("gate rejects a lambda sitting on its guard rail", () =>
        {
            var c = Cfg();
            TestRunner.True(!gate.Evaluate(Good() with { LambdaHome = c.LambdaMax }).Eligible, "upper rail accepted");
            TestRunner.True(!gate.Evaluate(Good() with { LambdaAway = c.LambdaMin }).Eligible, "lower rail accepted");
            var res = gate.Evaluate(Good() with { LambdaHome = c.LambdaMax });
            TestRunner.True(res.Reason.Contains("MODEL_STATE_AT_GUARD_RAIL"), $"wrong reason: {res.Reason}");

            // just inside the rail is fine: the gate refuses saturation, not high expectation
            TestRunner.True(gate.Evaluate(Good() with { LambdaHome = c.LambdaMax - 0.01 }).Eligible,
                "a high but unsaturated lambda must pass");
        });

        r.Add("gate rejects an invalid model state", () =>
        {
            TestRunner.True(!gate.Evaluate(Good() with { LambdaHome = double.NaN }).Eligible, "NaN lambda accepted");
            TestRunner.True(!gate.Evaluate(Good() with { ProbDraw = double.PositiveInfinity }).Eligible, "infinite probability accepted");
            TestRunner.True(!gate.Evaluate(Good() with { ProbHome = -0.1, ProbDraw = 0.85, ProbAway = 0.25 }).Eligible,
                "negative probability accepted");
        });

        r.Add("gate rejects probabilities that are not a distribution", () =>
        {
            var res = gate.Evaluate(Good() with { ProbHome = 0.5, ProbDraw = 0.3, ProbAway = 0.3 });
            TestRunner.True(!res.Eligible, "a triple summing to 1.1 was accepted");
            TestRunner.True(res.Reason.Contains("PROBABILITY_NOT_NORMALISED"), $"wrong reason: {res.Reason}");
        });

        // ---------------------------------------------------------------- no leakage

        r.Add("gate rejects evidence dated at or after the match", () =>
        {
            var sameDay = gate.Evaluate(Good() with { EvidenceCutoff = DateOnly.Parse("2024-03-01") });
            TestRunner.True(!sameDay.Eligible, "same-day evidence was accepted");
            TestRunner.True(sameDay.Reason.Contains("EVIDENCE_NOT_STRICTLY_PRE_MATCH"), $"wrong reason: {sameDay.Reason}");

            var future = gate.Evaluate(Good() with { EvidenceCutoff = DateOnly.Parse("2024-03-05") });
            TestRunner.True(!future.Eligible, "future evidence was accepted");
        });

        r.Add("all failing checks are reported, not just the first", () =>
        {
            var res = gate.Evaluate(Good() with
            {
                AwayIdentityConfidence = "AMBIGUOUS",
                AwayPriorMatches = 0,
                AwayEffectiveMatches = 0,
                CompetitionMatchesObserved = 3
            });
            TestRunner.True(res.Codes.Count >= 3, $"only {res.Codes.Count} reasons reported: {res.Reason}");
            TestRunner.True(res.Reason.Contains("IDENTITY_UNCONFIRMED") && res.Reason.Contains("NO_TEAM_HISTORY")
                && res.Reason.Contains("COMPETITION_NOT_COVERED"), $"missing a reason: {res.Reason}");
        });

        // ---------------------------------------------------------------- the DTO contract

        r.Add("a refused match publishes no probability at all", () =>
        {
            var svc = new PredictionService(Cfg());
            var dto = svc.Build(
                Raw(new ProbTriple(0.5, 0.25, 0.25, 1e-6), DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-02-24")),
                Snap("HOME", 40, 40, 0), null, "CONFIRMED", "CONFIRMED", 5000);

            TestRunner.True(!dto.PredictionEligible, "a match with a missing snapshot was published");
            TestRunner.True(dto.HomeProbability is null && dto.DrawProbability is null && dto.AwayProbability is null,
                "a refused match still carried probabilities");
            TestRunner.True(dto.GateReason != "OK", "a refused match reported OK");
        });

        r.Add("an accepted match publishes the model output unchanged", () =>
        {
            var svc = new PredictionService(Cfg());
            var p = new ProbTriple(0.6523, 0.2141, 0.1336, 1e-6);
            var dto = svc.Build(
                Raw(p, DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-02-24")),
                Snap("HOME", 40, 40, 0), Snap("AWAY", 35, 35, 0), "CONFIRMED", "CONFIRMED", 5000);

            TestRunner.True(dto.PredictionEligible, $"a good match was refused: {dto.GateReason}");
            TestRunner.Equal(p.Home, dto.HomeProbability!.Value, 0.0, "home probability was altered");
            TestRunner.Equal(p.Draw, dto.DrawProbability!.Value, 0.0, "draw probability was altered");
            TestRunner.Equal(p.Away, dto.AwayProbability!.Value, 0.0, "away probability was altered");
            TestRunner.Equal(1.0, dto.PublishedSum, 1e-12, "published sum");
        });

        r.Add("an extreme probability is published unclipped", () =>
        {
            var svc = new PredictionService(Cfg());
            foreach (var home in new[] { 0.90, 0.95, 0.9873 })
            {
                var rest = (1.0 - home) / 2.0;
                var p = new ProbTriple(home, rest, rest, 1e-6);
                var dto = svc.Build(
                    Raw(p, DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-02-24")),
                    Snap("HOME", 40, 40, 0), Snap("AWAY", 35, 35, 0), "CONFIRMED", "CONFIRMED", 5000);

                TestRunner.True(dto.PredictionEligible, "a confident prediction was refused for being confident");
                TestRunner.Equal(p.Home, dto.HomeProbability!.Value, 0.0,
                    $"a {home:0.00} probability was clipped to {dto.HomeProbability}");
            }
        });

        // ---------------------------------------------------------------- confidence

        r.Add("confidence is set by the weaker side, not the stronger one", () =>
        {
            var c = new ConfidenceClassifier(Cfg());
            var rich = c.Classify(new ConfidenceInput(400, 400, 400, 400, 0, 0, 5000));
            var mixed = c.Classify(new ConfidenceInput(400, 2, 400, 2, 0, 0, 5000));
            TestRunner.Equal(ConfidenceClass.High, rich, "two rich sides");
            TestRunner.True(mixed < ConfidenceClass.Medium,
                $"a debutant opponent must cap confidence, got {mixed}");
        });

        r.Add("confidence does not depend on the probability", () =>
        {
            var svc = new PredictionService(Cfg());
            var home = Snap("HOME", 40, 40, 0);
            var away = Snap("AWAY", 35, 35, 0);

            var sure = svc.Build(Raw(new ProbTriple(0.93, 0.04, 0.03, 1e-6),
                DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-02-24")), home, away, "CONFIRMED", "CONFIRMED", 5000);
            var unsure = svc.Build(Raw(new ProbTriple(0.34, 0.33, 0.33, 1e-6),
                DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-02-24")), home, away, "CONFIRMED", "CONFIRMED", 5000);

            TestRunner.Equal(sure.ConfidenceClass, unsure.ConfidenceClass,
                "the same evidence produced different confidence for different probabilities");
            TestRunner.Equal(ConfidenceClass.High, sure.ConfidenceClass, "well evidenced match");
        });

        r.Add("thin evidence lowers confidence even when the model is sure", () =>
        {
            var svc = new PredictionService(Cfg());
            var dto = svc.Build(Raw(new ProbTriple(0.93, 0.04, 0.03, 1e-6),
                    DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-02-24")),
                Snap("HOME", 40, 40, 0), Snap("AWAY", 2, 2, 0.0, "Limited"), "CONFIRMED", "CONFIRMED", 5000);

            TestRunner.True(dto.PredictionEligible, $"refused: {dto.GateReason}");
            TestRunner.Equal(ConfidenceClass.Low, dto.ConfidenceClass, "a 93% call on two matches of evidence");
            TestRunner.Equal(0.93, dto.HomeProbability!.Value, 1e-9, "and the probability itself is untouched");
        });

        r.Add("a heavily pooled side caps confidence", () =>
        {
            var c = new ConfidenceClassifier(Cfg());
            var known = c.Classify(new ConfidenceInput(50, 50, 50, 50, 0.0, 0.0, 5000));
            var pooled = c.Classify(new ConfidenceInput(50, 50, 50, 50, 0.0, 0.9, 5000));
            TestRunner.Equal(ConfidenceClass.High, known, "own evidence");
            TestRunner.Equal(ConfidenceClass.Low, pooled, "mostly prior");
        });

        r.Add("stale evidence lowers confidence through the decayed count", () =>
        {
            var c = new ConfidenceClassifier(Cfg());
            var fresh = c.Classify(new ConfidenceInput(50, 50, 50, 50, 0, 0, 5000));
            var stale = c.Classify(new ConfidenceInput(50, 50, 50, 1.5, 0, 0, 5000));
            TestRunner.Equal(ConfidenceClass.High, fresh, "fresh evidence");
            TestRunner.True(stale < fresh, $"decayed evidence must lower confidence, got {stale}");
        });

        r.Add("a published prediction never declares that it has no evidence", () =>
        {
            var svc = new PredictionService(Cfg());
            // one prior match, decayed just below the integer threshold - the case that produced
            // ConfidenceClass.None on 509 published predictions before the floor was added
            var dto = svc.Build(Raw(new ProbTriple(0.5, 0.25, 0.25, 1e-6),
                    DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-02-24")),
                Snap("HOME", 40, 40, 0), Snap("AWAY", 1, 0.978, 0.0, "Limited"), "CONFIRMED", "CONFIRMED", 5000);

            TestRunner.True(dto.PredictionEligible, $"refused: {dto.GateReason}");
            TestRunner.True(dto.ConfidenceClass != ConfidenceClass.None,
                "a published prediction claimed to have no evidence behind it");
            TestRunner.Equal(ConfidenceClass.Low, dto.ConfidenceClass, "one decayed match of evidence");
        });

        r.Add("NONE confidence is reserved for genuinely absent evidence", () =>
        {
            var c = new ConfidenceClassifier(Cfg());
            TestRunner.Equal(ConfidenceClass.None, c.Classify(new ConfidenceInput(40, 0, 40, 0, 0, 1.0, 5000)),
                "a side with zero matches");
        });

        // ---------------------------------------------------------------- determinism and logging

        r.Add("the same inputs produce a byte-identical DTO", () =>
        {
            var svc = new PredictionService(Cfg());
            var p = new ProbTriple(0.4123, 0.2811, 0.3066, 1e-6);
            string Build() => svc.Build(Raw(p, DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-02-24")),
                Snap("HOME", 40, 40, 0), Snap("AWAY", 35, 35, 0), "CONFIRMED", "CONFIRMED", 5000).ToCsv();
            TestRunner.Equal(Build(), Build(), "the DTO is not deterministic");
        });

        r.Add("the log round-trips a prediction and settles it against a real result", () =>
        {
            var svc = new PredictionService(Cfg());
            var dto = svc.Build(Raw(new ProbTriple(0.55, 0.25, 0.20, 1e-6),
                    DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-02-24")),
                Snap("HOME", 40, 40, 0), Snap("AWAY", 35, 35, 0), "CONFIRMED", "CONFIRMED", 5000);

            var log = new List<PredictionLogRecord> { PredictionLogRecord.From(dto) };
            TestRunner.True(log[0].LogLoss is null, "an unsettled row must not report a loss");

            var results = new Dictionary<string, (Outcome, int, int, DateOnly)>
            { ["M1"] = (Outcome.HomeWin, 2, 0, DateOnly.Parse("2024-03-01")) };
            var rep = PredictionSettlement.Settle(log, results);

            TestRunner.Equal(1, rep.Settled, "settled count");
            TestRunner.Equal(-Math.Log(0.55), log[0].LogLoss!.Value, 1e-12, "settled log loss");

            var again = PredictionSettlement.Settle(log, results);
            TestRunner.Equal(0, again.Settled, "a settled row must not settle twice");
            TestRunner.Equal(1, again.AlreadySettled, "and must say so");
        });

        r.Add("a refused prediction is logged, with no probability and a reason", () =>
        {
            var svc = new PredictionService(Cfg());
            var dto = svc.Build(Raw(new ProbTriple(0.55, 0.25, 0.20, 1e-6),
                    DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-02-24")),
                Snap("HOME", 40, 40, 0), Snap("AWAY", 0, 0, 1.0, "NoHistory"), "CONFIRMED", "CONFIRMED", 5000);
            var rec = PredictionLogRecord.From(dto);

            TestRunner.True(rec.HomeProbability is null, "a refused prediction logged a probability");
            TestRunner.True(rec.GateStatus.Contains("NO_TEAM_HISTORY"), $"gate status: {rec.GateStatus}");

            var results = new Dictionary<string, (Outcome, int, int, DateOnly)>
            { ["M1"] = (Outcome.HomeWin, 2, 0, DateOnly.Parse("2024-03-01")) };
            PredictionSettlement.Settle(new[] { rec }, results);
            TestRunner.True(rec.LogLoss is null, "a refused prediction must never produce a score");
        });

        r.Add("settlement refuses a result dated before the prediction", () =>
        {
            var svc = new PredictionService(Cfg());
            var dto = svc.Build(Raw(new ProbTriple(0.55, 0.25, 0.20, 1e-6),
                    DateOnly.Parse("2024-03-01"), DateOnly.Parse("2024-02-24")),
                Snap("HOME", 40, 40, 0), Snap("AWAY", 35, 35, 0), "CONFIRMED", "CONFIRMED", 5000);
            var log = new List<PredictionLogRecord> { PredictionLogRecord.From(dto) };

            var results = new Dictionary<string, (Outcome, int, int, DateOnly)>
            { ["M1"] = (Outcome.HomeWin, 2, 0, DateOnly.Parse("2024-02-01")) };
            var rep = PredictionSettlement.Settle(log, results);

            TestRunner.Equal(1, rep.RejectedAsEarly, "a result older than the prediction must be rejected");
            TestRunner.True(log[0].ActualOutcome is null, "and must not be attached");
        });

        // ---------------------------------------------------------------- competition coverage

        r.Add("competition coverage counts only strictly earlier days", () =>
        {
            var ms = new List<MatchRecord>();
            for (var i = 0; i < 6; i++)
                ms.Add(new MatchRecord
                {
                    MatchId = $"m{i}",
                    Date = DateOnly.Parse("2018-01-01").AddDays(i / 2),   // two matches per day
                    Season = "2017/18",
                    Competition = "X",
                    CompetitionType = "DOMESTIC_LEAGUE",
                    HomeTeamId = "A", AwayTeamId = "B", HomeTeamName = "A", AwayTeamName = "B",
                    HomeGoals = 1, AwayGoals = 0, MatchStatus = "FT", IdentityConfidence = "CONFIRMED"
                });
            var cov = CompetitionCoverage.Build(ms);
            TestRunner.Equal(0, cov.For("m0"), "first day, first match");
            TestRunner.Equal(0, cov.For("m1"), "first day, second match must not see its same-day sibling");
            TestRunner.Equal(2, cov.For("m2"), "second day sees exactly the first day");
            TestRunner.Equal(2, cov.For("m3"), "second day, second match");
            TestRunner.Equal(4, cov.For("m4"), "third day");
        });

        // ---------------------------------------------------------------- real data

        if (datasetPath is null || validatedTsPath is null || dcConfigPath is null) return;

        r.Add("real data: the output layer reproduces the validated V2 numbers exactly", () =>
        {
            var ts = TeamStrengthConfig.Load(validatedTsPath);
            var dc = DixonColesConfig.Load(dcConfigPath);
            var split = splitPath is not null ? SplitConfig.Load(splitPath) : SplitConfig.Load("(none)");
            var matches = MatchCsvReader.Read(datasetPath, ts).Matches;

            var built = new TeamStrengthService(ts).Build(matches);
            var rows = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side), StrengthPipeline.ToRow);
            var snaps = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side));

            var preds = new List<MatchPrediction>();
            new BacktestV2(dc, new ModelParameters { Rho = 0.0, BivariateC = 0.0 }, split)
                .Run(matches, rows, ModelMask.IndependentPoisson, preds.Add);

            var coverage = CompetitionCoverage.Build(matches);
            var svc = new PredictionService(Cfg());
            var identity = matches.ToDictionary(m => m.MatchId, m => m.IdentityConfidence, StringComparer.Ordinal);

            double Ll(Segment seg)
            {
                var sum = 0.0; var n = 0;
                foreach (var raw in preds.Where(p => p.Segment == seg))
                {
                    snaps.TryGetValue((raw.MatchId, "HOME"), out var h);
                    snaps.TryGetValue((raw.MatchId, "AWAY"), out var a);
                    var dto = svc.Build(raw, h, a, identity[raw.MatchId], identity[raw.MatchId], coverage.For(raw.MatchId));
                    var p = raw.Actual switch
                    {
                        Outcome.HomeWin => dto.ModelHomeProbability,
                        Outcome.Draw => dto.ModelDrawProbability,
                        _ => dto.ModelAwayProbability
                    };
                    sum += -Math.Log(Math.Max(p, 1e-15));
                    n++;
                }
                return sum / n;
            }

            TestRunner.Equal(33901, preds.Count, "prediction count");
            TestRunner.Equal(0.987473, Ll(Segment.Validation), 5e-6, "validation log loss through the DTO layer");
            TestRunner.Equal(0.993544, Ll(Segment.Test), 5e-6, "test log loss through the DTO layer");
        });
    }
}
