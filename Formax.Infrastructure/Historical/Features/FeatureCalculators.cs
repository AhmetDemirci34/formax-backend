using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Infrastructure.Historical.Features;

/// <summary>
/// Saf, paylaşılan feature hesaplayıcıları (deterministik, IO'suz). Hem per-match
/// <see cref="HistoricalFeatureService"/> hem batch <c>FeatureStoreBuilder</c> AYNI mantığı kullanır
/// (DRY). Girdi her zaman "hedef maç TARİHİNDEN ÖNCEki" maçlardır → leakage yok.
/// </summary>
public static class FeatureCalculators
{
    public const int WindowRecent = 10;

    /// <summary>Bir takımın maçı, kendi perspektifine normalize (en yeni önce).</summary>
    public readonly record struct TeamMatch(DateTime Date, bool IsHome, int Gf, int Ga, int? Shots, int? Target, int? OppShots, int OpponentId)
    {
        public char Result => Gf > Ga ? 'W' : Gf == Ga ? 'D' : 'L';
    }

    public static TeamFeatures BuildTeamFeatures(IReadOnlyList<TeamMatch> prior, double? elo, DateTime matchDate, int leaguePoints, int? leaguePosition)
    {
        var recent = prior.Take(WindowRecent).ToList();
        var homeMatches = prior.Where(x => x.IsHome).Take(WindowRecent).ToList();
        var awayMatches = prior.Where(x => !x.IsHome).Take(WindowRecent).ToList();

        return new TeamFeatures
        {
            Last3Form = Form(prior, 3),
            Last5Form = Form(prior, 5),
            Last10Form = Form(prior, 10),
            WinRate = Rate(recent, x => x.Result == 'W'),
            HomeWinRate = Rate(homeMatches, x => x.Result == 'W'),
            AwayWinRate = Rate(awayMatches, x => x.Result == 'W'),
            GoalsScoredAverage = Avg(recent, x => x.Gf),
            GoalsConcededAverage = Avg(recent, x => x.Ga),
            GoalDifference = Avg(recent, x => x.Gf - x.Ga),
            BTTSRate = Rate(recent, x => x.Gf > 0 && x.Ga > 0),
            Over25Rate = Rate(recent, x => x.Gf + x.Ga > 2),
            CleanSheetRate = Rate(recent, x => x.Ga == 0),
            FailedToScoreRate = Rate(recent, x => x.Gf == 0),
            WinningStreak = Streak(prior, x => x.Result == 'W'),
            LosingStreak = Streak(prior, x => x.Result == 'L'),
            UnbeatenStreak = Streak(prior, x => x.Result != 'L'),
            HomeGoalsAverage = Avg(homeMatches, x => x.Gf),
            AwayGoalsAverage = Avg(awayMatches, x => x.Gf),
            DaysSinceLastMatch = prior.Count > 0 ? (int?)(matchDate - prior[0].Date).Days : null,
            LeaguePosition = leaguePosition,
            LeaguePoints = leaguePoints,
            ShotAccuracy = ShotAccuracy(recent),
            ShotDifference = ShotDifference(recent),
            OffensiveRating = OffensiveRating(recent),
            DefensiveRating = DefensiveRating(recent),
            RecentMomentum = RecentMomentum(prior),
            Elo = elo,
            SampleSize = prior.Count
        };
    }

    /// <summary>Ev sahibi takımın geçmişinden, karşı takıma (awayId) karşı önceki H2H.</summary>
    public static (double winRate, double goalsAvg, int n) HeadToHead(IReadOnlyList<TeamMatch> homePrior, int awayId)
    {
        var meetings = homePrior.Where(x => x.OpponentId == awayId).ToList();
        if (meetings.Count == 0) return (0d, 0d, 0);
        var wins = meetings.Count(x => x.Result == 'W');
        var goals = meetings.Average(x => x.Gf + x.Ga);
        return (wins / (double)meetings.Count, goals, meetings.Count);
    }

    private static double Points(char r) => r == 'W' ? 3 : r == 'D' ? 1 : 0;

    private static double Form(IReadOnlyList<TeamMatch> prior, int n)
    {
        var take = prior.Take(n).ToList();
        return take.Count == 0 ? 0d : take.Sum(x => Points(x.Result)) / (take.Count * 3d);
    }

    private static double Rate(List<TeamMatch> list, Func<TeamMatch, bool> pred)
        => list.Count == 0 ? 0d : list.Count(pred) / (double)list.Count;

    private static double Avg(List<TeamMatch> list, Func<TeamMatch, int> sel)
        => list.Count == 0 ? 0d : list.Average(sel);

    private static int Streak(IReadOnlyList<TeamMatch> prior, Func<TeamMatch, bool> keep)
    {
        var n = 0;
        foreach (var x in prior) { if (keep(x)) n++; else break; }
        return n;
    }

    private static double? ShotAccuracy(List<TeamMatch> list)
    {
        var with = list.Where(x => x.Shots is > 0 && x.Target != null).ToList();
        if (with.Count == 0) return null;
        double shots = with.Sum(x => x.Shots!.Value), target = with.Sum(x => x.Target!.Value);
        return shots <= 0 ? null : target / shots;
    }

    private static double? ShotDifference(List<TeamMatch> list)
    {
        var with = list.Where(x => x.Shots != null && x.OppShots != null).ToList();
        return with.Count == 0 ? null : with.Average(x => x.Shots!.Value - x.OppShots!.Value);
    }

    private static double OffensiveRating(List<TeamMatch> recent)
    {
        if (recent.Count == 0) return 0d;
        var gs = Avg(recent, x => x.Gf);
        var over = Rate(recent, x => x.Gf + x.Ga > 2);
        var acc = ShotAccuracy(recent) ?? 0.5;
        return 0.6 * Math.Min(gs / 3d, 1d) + 0.25 * over + 0.15 * acc;
    }

    private static double DefensiveRating(List<TeamMatch> recent)
    {
        if (recent.Count == 0) return 0d;
        var cs = Rate(recent, x => x.Ga == 0);
        var ga = Avg(recent, x => x.Ga);
        return 0.5 * cs + 0.5 * (1d / (1d + ga));
    }

    private static double RecentMomentum(IReadOnlyList<TeamMatch> prior)
    {
        var take = prior.Take(5).ToList();
        if (take.Count == 0) return 0d;
        double num = 0, den = 0;
        for (var i = 0; i < take.Count; i++)
        {
            var w = take.Count - i;
            num += w * Points(take[i].Result);
            den += w * 3d;
        }
        return den == 0 ? 0d : num / den;
    }
}
