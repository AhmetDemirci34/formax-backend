using Formax.DixonColes.Config;
using Formax.DixonColes.Models;
using Formax.DixonColes.Services;
using Formax.TeamStrength.Models;
using Formax.TeamStrength.Tests;

namespace Formax.DixonColes.Tests;

public static class BacktestTests
{
    private static DixonColesConfig Cfg() => new();

    private static MatchRecord M(string id, string date, string home, string away, int hg, int ag,
        string type = "DOMESTIC_LEAGUE") => new()
        {
            MatchId = id,
            Date = DateOnly.ParseExact(date, "yyyy-MM-dd"),
            Season = "2020/21",
            Competition = "Premier League",
            CompetitionType = type,
            HomeTeamId = home, AwayTeamId = away,
            HomeTeamName = home, AwayTeamName = away,
            HomeGoals = hg, AwayGoals = ag,
            MatchStatus = "FT", IdentityConfidence = "CONFIRMED"
        };

    private static StrengthRow S(string matchId, string side, string date, double atk, double def,
        DateOnly? last = null, int used = 10) => new()
        {
            MatchId = matchId, TeamId = side + matchId, Side = side,
            MatchDate = DateOnly.ParseExact(date, "yyyy-MM-dd"),
            Attack = atk, Defense = def, Overall = atk / def,
            MatchesUsed = used, Confidence = "High", ColdStartClass = "Rich",
            PriorSource = "POOL:DOMESTIC_LEAGUE", PriorWeight = 0.2,
            LastMatchDate = last
        };

    public static void Register(TestRunner t, string? datasetPath, string? snapshotPath)
    {
        // ------------------------------------------------------------------ maths
        t.Add("dixon-coles: outcome probabilities sum to exactly 1", () =>
        {
            var cfg = Cfg();
            foreach (var (l, m) in new[] { (1.4, 1.1), (0.3, 2.7), (3.5, 0.2), (0.05, 0.05), (6.0, 6.0) })
            {
                var p = DixonColesModel.Outcome1X2(l, m, cfg);
                TestRunner.Equal(1.0, p.Sum, 1e-12, $"sum for lambda={l}, mu={m}");
                TestRunner.True(p.Home > 0 && p.Draw > 0 && p.Away > 0, "every outcome must be positive");
            }
        });

        t.Add("dixon-coles: the score grid itself is a normalised distribution", () =>
        {
            var cfg = Cfg();
            var grid = DixonColesModel.ScoreGrid(1.6, 1.2, cfg);
            var total = grid.Sum(row => row.Sum());
            TestRunner.Equal(1.0, total, 1e-12, "grid mass");
        });

        t.Add("dixon-coles: tau only touches the four low scores", () =>
        {
            const double l = 1.5, m = 1.2, rho = -0.05;
            TestRunner.Equal(1.0 - l * m * rho, DixonColesModel.Tau(0, 0, l, m, rho), 1e-12, "tau(0,0)");
            TestRunner.Equal(1.0 + l * rho, DixonColesModel.Tau(0, 1, l, m, rho), 1e-12, "tau(0,1)");
            TestRunner.Equal(1.0 + m * rho, DixonColesModel.Tau(1, 0, l, m, rho), 1e-12, "tau(1,0)");
            TestRunner.Equal(1.0 - rho, DixonColesModel.Tau(1, 1, l, m, rho), 1e-12, "tau(1,1)");
            TestRunner.Equal(1.0, DixonColesModel.Tau(2, 1, l, m, rho), 1e-12, "tau(2,1) untouched");
            TestRunner.Equal(1.0, DixonColesModel.Tau(0, 2, l, m, rho), 1e-12, "tau(0,2) untouched");
        });

        t.Add("dixon-coles: rho=0 reproduces independent Poisson exactly", () =>
        {
            var cfg = Cfg(); cfg.Rho = 0.0;
            var a = DixonColesModel.Outcome1X2(1.7, 1.1, cfg);
            var b = DixonColesModel.Outcome1X2IndependentPoisson(1.7, 1.1, cfg);
            TestRunner.Equal(a.Home, b.Home, 1e-12, "home");
            TestRunner.Equal(a.Draw, b.Draw, 1e-12, "draw");
        });

        t.Add("dixon-coles: negative rho lifts the draw probability", () =>
        {
            var indep = Cfg(); indep.Rho = 0.0;
            var dc = Cfg(); dc.Rho = -0.10;
            var p0 = DixonColesModel.Outcome1X2(1.3, 1.1, indep);
            var p1 = DixonColesModel.Outcome1X2(1.3, 1.1, dc);
            TestRunner.Greater(p1.Draw, p0.Draw, "the low-score correction must raise P(draw)");
        });

        t.Add("stronger team gets the higher probability", () =>
        {
            var cfg = Cfg();
            var strong = DixonColesModel.Outcome1X2(2.2, 0.8, cfg);
            TestRunner.Greater(strong.Home, strong.Away, "a much larger home lambda must dominate");
        });

        // ------------------------------------------------------------------ metrics
        t.Add("metrics: a perfect forecast scores 0 and a certain miss scores badly", () =>
        {
            var perfect = new MetricAccumulator("m", "s", "g");
            perfect.Add(new ProbTriple(1, 0, 0, 1e-12), Outcome.HomeWin);
            TestRunner.Equal(0.0, perfect.Brier, 1e-9, "brier of a perfect forecast");
            TestRunner.Equal(0.0, perfect.Rps, 1e-9, "rps of a perfect forecast");
            TestRunner.Equal(1.0, perfect.Accuracy, 1e-12, "accuracy");
            TestRunner.True(perfect.LogLoss < 1e-9, "log loss of a perfect forecast");

            var wrong = new MetricAccumulator("m", "s", "g");
            wrong.Add(new ProbTriple(0.98, 0.01, 0.01, 1e-12), Outcome.AwayWin);
            TestRunner.Greater(wrong.LogLoss, 3.0, "a confident miss must be punished");
        });

        t.Add("metrics: uniform forecast has log loss ln(3)", () =>
        {
            var acc = new MetricAccumulator("m", "s", "g");
            var third = 1.0 / 3.0;
            acc.Add(new ProbTriple(third, third, third, 1e-12), Outcome.Draw);
            TestRunner.Equal(Math.Log(3), acc.LogLoss, 1e-9, "log loss");
            TestRunner.Equal(2.0 / 3.0, acc.Brier, 1e-9, "brier");
        });

        t.Add("metrics: RPS punishes distance on the ordered scale", () =>
        {
            // predicting AwayWin when HomeWin happens must be worse than predicting Draw
            var near = new MetricAccumulator("m", "s", "g");
            near.Add(new ProbTriple(0.0, 1.0, 0.0, 1e-12), Outcome.HomeWin);
            var far = new MetricAccumulator("m", "s", "g");
            far.Add(new ProbTriple(0.0, 0.0, 1.0, 1e-12), Outcome.HomeWin);
            TestRunner.Greater(far.Rps, near.Rps, "an ordered metric must penalise the further class more");
        });

        // ------------------------------------------------------------------ leakage
        t.Add("leakage: changing a LATER result cannot change an EARLIER prediction", () =>
        {
            var cfg = Cfg();
            var snaps = new Dictionary<(string, string), StrengthRow>
            {
                { ("M1","HOME"), S("M1","HOME","2020-08-01",1.2,0.9) },
                { ("M1","AWAY"), S("M1","AWAY","2020-08-01",1.0,1.0) },
                { ("M2","HOME"), S("M2","HOME","2020-08-08",1.1,1.0, DateOnly.ParseExact("2020-08-01","yyyy-MM-dd")) },
                { ("M2","AWAY"), S("M2","AWAY","2020-08-08",1.0,1.1, DateOnly.ParseExact("2020-08-01","yyyy-MM-dd")) },
                { ("M3","HOME"), S("M3","HOME","2020-08-15",1.0,1.0, DateOnly.ParseExact("2020-08-08","yyyy-MM-dd")) },
                { ("M3","AWAY"), S("M3","AWAY","2020-08-15",1.0,1.0, DateOnly.ParseExact("2020-08-08","yyyy-MM-dd")) }
            };
            var baseMatches = new List<MatchRecord>
            { M("M1","2020-08-01","A","B",1,0), M("M2","2020-08-08","C","D",2,2), M("M3","2020-08-15","E","F",0,1) };
            var mutated = new List<MatchRecord>
            { M("M1","2020-08-01","A","B",1,0), M("M2","2020-08-08","C","D",2,2), M("M3","2020-08-15","E","F",7,0) };

            var r1 = new WalkForwardBacktest(cfg).Run(baseMatches, snaps);
            var r2 = new WalkForwardBacktest(cfg).Run(mutated, snaps);
            var p1 = r1.ByModel[WalkForwardBacktest.DixonColesModelName].Single(p => p.MatchId == "M2");
            var p2 = r2.ByModel[WalkForwardBacktest.DixonColesModelName].Single(p => p.MatchId == "M2");
            TestRunner.Equal(p1.Probabilities.Home, p2.Probabilities.Home, 1e-15, "home probability");
            TestRunner.Equal(p1.Probabilities.Draw, p2.Probabilities.Draw, 1e-15, "draw probability");
        });

        t.Add("leakage: two matches on the same day cannot inform each other", () =>
        {
            var cfg = Cfg();
            var snaps = new Dictionary<(string, string), StrengthRow>
            {
                { ("D1","HOME"), S("D1","HOME","2020-08-01",1.0,1.0) },
                { ("D1","AWAY"), S("D1","AWAY","2020-08-01",1.0,1.0) },
                { ("D2","HOME"), S("D2","HOME","2020-08-01",1.0,1.0) },
                { ("D2","AWAY"), S("D2","AWAY","2020-08-01",1.0,1.0) }
            };
            var list = new List<MatchRecord>
            { M("D1","2020-08-01","A","B",6,0), M("D2","2020-08-01","C","D",0,0) };
            var r = new WalkForwardBacktest(cfg).Run(list, snaps);
            var preds = r.ByModel[WalkForwardBacktest.DixonColesModelName];
            var a = preds.Single(p => p.MatchId == "D1");
            var b = preds.Single(p => p.MatchId == "D2");
            TestRunner.Equal(a.LambdaHome, b.LambdaHome, 1e-15, "same-day matches must share identical context");
            TestRunner.Equal(a.Probabilities.Home, b.Probabilities.Home, 1e-15, "same-day probabilities must match");
        });

        t.Add("leakage: the backtest reports zero input violations on synthetic data", () =>
        {
            var cfg = Cfg();
            var snaps = new Dictionary<(string, string), StrengthRow>
            {
                { ("X1","HOME"), S("X1","HOME","2020-08-01",1.0,1.0) },
                { ("X1","AWAY"), S("X1","AWAY","2020-08-01",1.0,1.0) }
            };
            var r = new WalkForwardBacktest(cfg).Run(new List<MatchRecord> { M("X1","2020-08-01","A","B",1,1) }, snaps);
            TestRunner.Equal(0, r.LeakageViolations, "leakage violations");
            TestRunner.Equal(0, r.NormalisationViolations, "normalisation violations");
        });

        // ------------------------------------------------------------------ real data
        if (datasetPath is not null && snapshotPath is not null &&
            File.Exists(datasetPath) && File.Exists(snapshotPath))
        {
            t.Add("real data: every prediction is normalised and leakage free", () =>
            {
                var cfg = Cfg();
                var read = Formax.TeamStrength.Services.MatchCsvReader.Read(datasetPath,
                    new Formax.TeamStrength.Config.TeamStrengthConfig());
                var snaps = StrengthSnapshotReader.Read(snapshotPath);
                var r = new WalkForwardBacktest(cfg).Run(read.Matches, snaps);

                TestRunner.Equal(0, r.LeakageViolations, "leakage violations on the real dataset");
                TestRunner.Equal(0, r.NormalisationViolations, "normalisation violations");
                TestRunner.Equal(0, r.MatchesSkippedNoSnapshot, "every match must have a snapshot");

                var dc = r.ByModel[WalkForwardBacktest.DixonColesModelName];
                TestRunner.True(dc.Count > 30000, $"expected the full dataset, got {dc.Count}");
                var bad = dc.Count(p => Math.Abs(p.Probabilities.Sum - 1.0) > 1e-9
                                        || double.IsNaN(p.Probabilities.Home)
                                        || p.Probabilities.Home <= 0 || p.Probabilities.Draw <= 0 || p.Probabilities.Away <= 0);
                TestRunner.Equal(0, bad, "no prediction may be unnormalised, NaN or non-positive");

                var cutoffViolations = dc.Count(p => p.EvidenceCutoff.HasValue && p.EvidenceCutoff.Value >= p.PredictionDate);
                TestRunner.Equal(0, cutoffViolations, "evidence must predate the match");
            });
        }
    }
}
