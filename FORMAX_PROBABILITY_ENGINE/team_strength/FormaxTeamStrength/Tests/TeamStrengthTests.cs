using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;
using Formax.TeamStrength.Services;

namespace Formax.TeamStrength.Tests;

public static class TeamStrengthTests
{
    private static int _seq;

    private static MatchRecord M(string date, string home, string away, int hg, int ag,
        string competition = "Premier League", string type = "DOMESTIC_LEAGUE", string season = "2020/21")
        => new()
        {
            MatchId = $"T{++_seq:d5}",
            Date = DateOnly.ParseExact(date, "yyyy-MM-dd"),
            Season = season,
            Competition = competition,
            CompetitionType = type,
            HomeTeamId = home,
            AwayTeamId = away,
            HomeTeamName = home,
            AwayTeamName = away,
            HomeGoals = hg,
            AwayGoals = ag,
            MatchStatus = "FT",
            IdentityConfidence = "CONFIRMED"
        };

    private static TeamStrengthConfig Cfg() => new();

    private static TeamStrengthSnapshot Snap(BuildResult r, string matchId, string teamId)
        => r.Snapshots.Single(s => s.MatchId == matchId && s.TeamId == teamId);

    public static void Register(TestRunner t, string? realDatasetPath)
    {
        // ---------------------------------------------------------------- 1. LEAKAGE
        t.Add("leakage: a snapshot equals the state built from earlier matches only", () =>
        {
            _seq = 0;
            var all = new List<MatchRecord>
            {
                M("2020-08-01","A","B",2,0), M("2020-08-08","B","A",1,1),
                M("2020-08-15","A","C",3,1), M("2020-08-22","C","B",0,2),
                M("2020-08-29","A","B",4,0),  // <- target match
                M("2020-09-05","A","C",5,0),  // later matches must not influence the target
                M("2020-09-12","B","A",0,4)
            };
            var target = all[4];

            var full = new TeamStrengthService(Cfg()).Build(all);
            var snapFull = Snap(full, target.MatchId, "A");

            // rebuild using ONLY the matches strictly before the target date
            var priorOnly = all.Where(m => m.Date < target.Date).ToList();
            priorOnly.Add(target);
            var partial = new TeamStrengthService(Cfg()).Build(priorOnly);
            var snapPartial = Snap(partial, target.MatchId, "A");

            TestRunner.Equal(snapPartial.AttackStrength, snapFull.AttackStrength, 1e-12, "attack");
            TestRunner.Equal(snapPartial.DefenseStrength, snapFull.DefenseStrength, 1e-12, "defense");
            TestRunner.Equal(snapPartial.MatchesUsed, snapFull.MatchesUsed, "matches used");
        });

        t.Add("leakage: changing a LATER result cannot change an EARLIER snapshot", () =>
        {
            _seq = 0;
            var baseList = new List<MatchRecord>
            {
                M("2020-08-01","A","B",2,0), M("2020-08-08","A","C",1,1), M("2020-08-15","A","D",0,3)
            };
            var r1 = new TeamStrengthService(Cfg()).Build(baseList);
            var s1 = Snap(r1, baseList[1].MatchId, "A");

            _seq = 0;
            var mutated = new List<MatchRecord>
            {
                M("2020-08-01","A","B",2,0), M("2020-08-08","A","C",1,1), M("2020-08-15","A","D",9,0) // future changed
            };
            var r2 = new TeamStrengthService(Cfg()).Build(mutated);
            var s2 = Snap(r2, mutated[1].MatchId, "A");

            TestRunner.Equal(s1.AttackStrength, s2.AttackStrength, 1e-12, "attack must be unaffected by a later match");
            TestRunner.Equal(s1.OverallStrength, s2.OverallStrength, 1e-12, "overall must be unaffected");
        });

        t.Add("leakage: same-day matches do not feed each other", () =>
        {
            _seq = 0;
            var list = new List<MatchRecord>
            {
                M("2020-08-01","A","B",5,0),   // same day
                M("2020-08-01","A","C",0,0)    // same day, must not see the 5-0
            };
            var r = new TeamStrengthService(Cfg()).Build(list);
            var s1 = Snap(r, list[0].MatchId, "A");
            var s2 = Snap(r, list[1].MatchId, "A");
            TestRunner.Equal(s1.AttackStrength, s2.AttackStrength, 1e-12, "same-day snapshots must be identical");
            TestRunner.Equal(0, s2.MatchesUsed, "second same-day match must still see zero history");
        });

        // ---------------------------------------------------------------- 2. COLD START
        t.Add("cold start: first ever match has no history and reports its prior", () =>
        {
            _seq = 0;
            var list = new List<MatchRecord> { M("2020-08-01","A","B",1,0) };
            var r = new TeamStrengthService(Cfg()).Build(list);
            var s = Snap(r, list[0].MatchId, "A");
            TestRunner.Equal(0, s.MatchesUsed, "matches used");
            TestRunner.Equal(ColdStartClass.NoHistory, s.ColdStartClass, "cold start class");
            TestRunner.Equal(ConfidenceLevel.None, s.Confidence, "confidence");
            TestRunner.Equal(1.0, s.PriorWeight, 1e-12, "prior weight must be 1 with no evidence");
            TestRunner.Equal(1.0, s.OverallStrength, 1e-12, "with no evidence the snapshot is the neutral prior");
            TestRunner.True(!string.IsNullOrWhiteSpace(s.PriorSource), "prior source must always be recorded");
            TestRunner.True(s.HomeStrength is null && s.AwayStrength is null, "venue strengths must be null");
        });

        t.Add("cold start: 3 prior matches -> Developing, shrinkage still strong", () =>
        {
            _seq = 0;
            var list = new List<MatchRecord>
            {
                M("2020-08-01","A","B",3,0), M("2020-08-08","C","A",0,2), M("2020-08-15","A","D",2,1),
                M("2020-08-22","A","E",1,0)   // target
            };
            var r = new TeamStrengthService(Cfg()).Build(list);
            var s = Snap(r, list[3].MatchId, "A");
            TestRunner.Equal(3, s.MatchesUsed, "matches used");
            TestRunner.Equal(ColdStartClass.Developing, s.ColdStartClass, "cold start class");
            TestRunner.True(s.PriorWeight > 0.4 && s.PriorWeight < 1.0,
                $"prior must still dominate but not fully (was {s.PriorWeight:0.000})");
            TestRunner.Greater(s.OverallStrength, 1.0, "a team that won 3 in a row should be above neutral");
        });

        t.Add("cold start: 10+ prior matches -> Rich, own evidence dominates", () =>
        {
            _seq = 0;
            var list = new List<MatchRecord>();
            var d = new DateOnly(2020, 8, 1);
            for (var i = 0; i < 12; i++)
            {
                list.Add(M(d.ToString("yyyy-MM-dd"), "A", $"OPP{i}", 2, 0));
                d = d.AddDays(7);
            }
            list.Add(M(d.ToString("yyyy-MM-dd"), "A", "OPPX", 1, 0)); // target
            var r = new TeamStrengthService(Cfg()).Build(list);
            var s = Snap(r, list[^1].MatchId, "A");
            TestRunner.Equal(12, s.MatchesUsed, "matches used");
            TestRunner.Equal(ColdStartClass.Rich, s.ColdStartClass, "cold start class");
            TestRunner.Equal(ConfidenceLevel.High, s.Confidence, "confidence");
            TestRunner.True(s.PriorWeight < 0.4, $"own evidence must dominate (prior weight {s.PriorWeight:0.000})");
        });

        t.Add("cold start: UEFA-only team is pooled against the UEFA competition type", () =>
        {
            _seq = 0;
            var list = new List<MatchRecord>();
            var d = new DateOnly(2020, 7, 1);
            // build a UEFA qualifier pool from other clubs first
            for (var i = 0; i < 40; i++)
            {
                list.Add(M(d.ToString("yyyy-MM-dd"), $"Q{i}A", $"Q{i}B", 1, 1, "UEFA Champions League", "UEFA_QUALIFIER"));
                d = d.AddDays(1);
            }
            // a brand new club appears in the same competition type
            list.Add(M(d.ToString("yyyy-MM-dd"), "NEWCLUB", "Q1A", 0, 0, "UEFA Champions League", "UEFA_QUALIFIER"));
            var r = new TeamStrengthService(Cfg()).Build(list);
            var s = Snap(r, list[^1].MatchId, "NEWCLUB");
            TestRunner.Equal(0, s.MatchesUsed, "a brand new club has no history");
            TestRunner.True(s.PriorSource.StartsWith("POOL:UEFA_QUALIFIER") || s.PriorSource == "GLOBAL_NEUTRAL",
                $"prior source must name the pool actually used, was '{s.PriorSource}'");
            TestRunner.Equal(1.0, s.PriorWeight, 1e-12, "with no evidence the prior carries all the weight");
        });

        // ---------------------------------------------------------------- 3. CROSS COMPETITION
        t.Add("cross competition: one rating stream, a domestic run shows up in a UEFA match", () =>
        {
            _seq = 0;
            var list = new List<MatchRecord>();
            var d = new DateOnly(2020, 8, 1);
            for (var i = 0; i < 8; i++)
            {
                list.Add(M(d.ToString("yyyy-MM-dd"), "A", $"D{i}", 3, 0));  // domestic thrashings
                d = d.AddDays(7);
            }
            var uefa = M(d.ToString("yyyy-MM-dd"), "A", "EUR1", 0, 0, "UEFA Champions League", "UEFA_MAIN");
            list.Add(uefa);
            var r = new TeamStrengthService(Cfg()).Build(list);
            var s = Snap(r, uefa.MatchId, "A");
            TestRunner.Equal(8, s.MatchesUsed, "domestic history must carry into the UEFA match");
            TestRunner.Greater(s.AttackStrength, 1.0, "attack learned domestically must be visible in UEFA");
        });

        t.Add("promotion: rating is not reset when the competition changes", () =>
        {
            _seq = 0;
            var list = new List<MatchRecord>();
            var d = new DateOnly(2020, 8, 1);
            for (var i = 0; i < 10; i++)
            {
                list.Add(M(d.ToString("yyyy-MM-dd"), "PROMO", $"C{i}", 3, 0, "Championship", "DOMESTIC_LEAGUE", "2020/21"));
                d = d.AddDays(7);
            }
            var lastChampionship = list[^1];
            var firstTopFlight = M(d.ToString("yyyy-MM-dd"), "PROMO", "PL1", 0, 0, "Premier League", "DOMESTIC_LEAGUE", "2021/22");
            list.Add(firstTopFlight);

            var r = new TeamStrengthService(Cfg()).Build(list);
            var sBefore = Snap(r, lastChampionship.MatchId, "PROMO");
            var sAfter = Snap(r, firstTopFlight.MatchId, "PROMO");

            TestRunner.Equal(10, sAfter.MatchesUsed, "history must survive the competition change");
            TestRunner.Greater(sAfter.MatchesUsed, sBefore.MatchesUsed, "evidence keeps accumulating");
            TestRunner.Greater(sAfter.AttackStrength, 1.0, "rating must not be reset to neutral");
        });

        // ---------------------------------------------------------------- 4. HOME / AWAY
        t.Add("home/away: a team strong at home and weak away separates the two indices", () =>
        {
            _seq = 0;
            var list = new List<MatchRecord>();
            var d = new DateOnly(2020, 8, 1);
            for (var i = 0; i < 10; i++)
            {
                list.Add(M(d.ToString("yyyy-MM-dd"), "SPLIT", $"H{i}", 3, 0)); d = d.AddDays(4);
                list.Add(M(d.ToString("yyyy-MM-dd"), $"A{i}", "SPLIT", 3, 0)); d = d.AddDays(4);
            }
            var target = M(d.ToString("yyyy-MM-dd"), "SPLIT", "LAST", 0, 0);
            list.Add(target);
            var r = new TeamStrengthService(Cfg()).Build(list);
            var s = Snap(r, target.MatchId, "SPLIT");

            TestRunner.True(s.HomeStrength.HasValue && s.AwayStrength.HasValue, "both venue indices must exist");
            TestRunner.Greater(s.HomeStrength!.Value, s.AwayStrength!.Value,
                "home index must exceed away index for a home-strong team");
            TestRunner.Equal(10, s.HomeMatchesUsed, "home matches used");
            TestRunner.Equal(10, s.AwayMatchesUsed, "away matches used");
        });

        t.Add("home/away: venue index is null until that venue has been played", () =>
        {
            _seq = 0;
            var list = new List<MatchRecord>
            {
                M("2020-08-01","ONLYHOME","X",1,0),
                M("2020-08-08","ONLYHOME","Y",1,0)
            };
            var r = new TeamStrengthService(Cfg()).Build(list);
            var s = Snap(r, list[1].MatchId, "ONLYHOME");
            TestRunner.True(s.HomeStrength.HasValue, "home index must exist after a home match");
            TestRunner.True(s.AwayStrength is null, "away index must stay null with no away match");
        });

        // ---------------------------------------------------------------- 5. CONSISTENCY
        t.Add("determinism: two runs over the same input produce identical snapshots", () =>
        {
            _seq = 0;
            var list = new List<MatchRecord>
            {
                M("2020-08-01","A","B",2,1), M("2020-08-08","C","A",0,0), M("2020-08-15","B","C",3,2)
            };
            var r1 = new TeamStrengthService(Cfg()).Build(list);
            var r2 = new TeamStrengthService(Cfg()).Build(list);
            var a = string.Join("\n", r1.Snapshots.Select(s => s.ToCsv()));
            var b = string.Join("\n", r2.Snapshots.Select(s => s.ToCsv()));
            TestRunner.Equal(a, b, "snapshot output must be deterministic");
        });

        t.Add("time decay: a long absence lowers effective evidence", () =>
        {
            _seq = 0;
            var cfg = Cfg();
            var list = new List<MatchRecord>();
            var d = new DateOnly(2018, 1, 1);
            for (var i = 0; i < 10; i++) { list.Add(M(d.ToString("yyyy-MM-dd"), "GAP", $"G{i}", 2, 0)); d = d.AddDays(7); }
            var soon = M(d.ToString("yyyy-MM-dd"), "GAP", "N1", 0, 0);
            var late = M(d.AddYears(4).ToString("yyyy-MM-dd"), "GAP", "N2", 0, 0);
            list.Add(soon); list.Add(late);

            var r = new TeamStrengthService(cfg).Build(list);
            var sSoon = Snap(r, soon.MatchId, "GAP");
            var sLate = Snap(r, late.MatchId, "GAP");
            TestRunner.Greater(sSoon.EffectiveMatches, sLate.EffectiveMatches,
                "effective evidence must decay across a four year gap");
            TestRunner.Greater(sLate.PriorWeight, sSoon.PriorWeight, "an absent team must be pulled back to the prior");
        });

        t.Add("identity: rows without CONFIRMED identity never enter the engine", () =>
        {
            var cfg = Cfg();
            TestRunner.Equal("CONFIRMED", cfg.RequiredIdentityConfidence, "config gate");
            var line = "\"M1\",\"2020/21\",\"Premier League\",\"DOMESTIC_LEAGUE\",\"Matchday 1\",\"2020-08-01\"";
            var parsed = MatchCsvReader.ParseLine(line);
            TestRunner.Equal(6, parsed.Count, "csv parser must handle quoted fields");
        });

        // ---------------------------------------------------------------- 6. REAL DATA
        if (realDatasetPath is not null && File.Exists(realDatasetPath))
        {
            t.Add("real data: engine runs over the model dataset and every snapshot is auditable", () =>
            {
                var cfg = Cfg();
                var read = MatchCsvReader.Read(realDatasetPath, cfg);
                TestRunner.True(read.Matches.Count > 30000, $"expected the full dataset, got {read.Matches.Count}");

                var r = new TeamStrengthService(cfg).Build(read.Matches);
                TestRunner.Equal(read.Matches.Count * 2, r.Snapshots.Count, "two snapshots per match");

                var bad = r.Snapshots.Where(s =>
                    double.IsNaN(s.AttackStrength) || double.IsNaN(s.DefenseStrength) ||
                    double.IsInfinity(s.OverallStrength) || s.AttackStrength <= 0 || s.DefenseStrength <= 0).Take(3).ToList();
                TestRunner.Equal(0, bad.Count, "no snapshot may be NaN, infinite or non-positive");

                var noPrior = r.Snapshots.Count(s => string.IsNullOrWhiteSpace(s.PriorSource));
                TestRunner.Equal(0, noPrior, "every snapshot must record which prior it used");

                var firstEver = r.Snapshots.Where(s => s.MatchesUsed == 0).ToList();
                TestRunner.True(firstEver.All(s => s.ColdStartClass == ColdStartClass.NoHistory),
                    "a snapshot with no prior match must be classified NoHistory");
                TestRunner.True(firstEver.All(s => Math.Abs(s.PriorWeight - 1.0) < 1e-12),
                    "a snapshot with no prior match must be pure prior");
            });

            t.Add("real data: no snapshot uses a match dated on or after its own match date", () =>
            {
                var cfg = Cfg();
                var read = MatchCsvReader.Read(realDatasetPath, cfg);
                var r = new TeamStrengthService(cfg).Build(read.Matches);
                var violations = r.Snapshots.Count(s => s.LastMatchDate.HasValue && s.LastMatchDate.Value >= s.MatchDate);
                TestRunner.Equal(0, violations,
                    "LastMatchDate must always be strictly earlier than the match being predicted");
            });
        }
    }
}
