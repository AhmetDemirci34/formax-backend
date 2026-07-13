using System.Collections.Generic;
using Formax.Infrastructure.Historical.Features;

namespace Formax.Infrastructure.Historical.Dataset;

/// <summary>
/// MatchFeatureVector → sabit-sıralı düz double[] (ML X). Sıra deterministiktir; null'lar 0.0 impute edilir
/// (belgeli). Feature isimleri model için tek gerçek şemadır.
/// </summary>
public static class FeatureFlattener
{
    private static readonly string[] TeamFeatureNames =
    {
        "Last3Form","Last5Form","Last10Form","WinRate","HomeWinRate","AwayWinRate",
        "GoalsScoredAverage","GoalsConcededAverage","GoalDifference","BTTSRate","Over25Rate",
        "CleanSheetRate","FailedToScoreRate","WinningStreak","LosingStreak","UnbeatenStreak",
        "HomeGoalsAverage","AwayGoalsAverage","DaysSinceLastMatch","LeaguePosition","LeaguePoints",
        "ShotAccuracy","ShotDifference","OffensiveRating","DefensiveRating","RecentMomentum","Elo"
    };

    public static IReadOnlyList<string> FeatureNames { get; } = BuildNames();

    public static int Count => FeatureNames.Count;

    private static List<string> BuildNames()
    {
        var names = new List<string>(TeamFeatureNames.Length * 2 + 5);
        foreach (var n in TeamFeatureNames) names.Add("Home_" + n);
        foreach (var n in TeamFeatureNames) names.Add("Away_" + n);
        names.Add("EloDifference");
        names.Add("HomeAdvantage");
        names.Add("HeadToHeadWinRate");
        names.Add("HeadToHeadGoalsAverage");
        names.Add("LeagueStrength");
        return names;
    }

    public static double[] Flatten(MatchFeatureVector v)
    {
        var x = new double[Count];
        var i = 0;
        WriteTeam(x, ref i, v.Home);
        WriteTeam(x, ref i, v.Away);
        x[i++] = v.EloDifference ?? 0d;
        x[i++] = v.HomeAdvantage;
        x[i++] = v.HeadToHeadWinRate;
        x[i++] = v.HeadToHeadGoalsAverage;
        x[i++] = v.LeagueStrength;
        return x;
    }

    private static void WriteTeam(double[] x, ref int i, TeamFeatures t)
    {
        x[i++] = t.Last3Form;
        x[i++] = t.Last5Form;
        x[i++] = t.Last10Form;
        x[i++] = t.WinRate;
        x[i++] = t.HomeWinRate;
        x[i++] = t.AwayWinRate;
        x[i++] = t.GoalsScoredAverage;
        x[i++] = t.GoalsConcededAverage;
        x[i++] = t.GoalDifference;
        x[i++] = t.BTTSRate;
        x[i++] = t.Over25Rate;
        x[i++] = t.CleanSheetRate;
        x[i++] = t.FailedToScoreRate;
        x[i++] = t.WinningStreak;
        x[i++] = t.LosingStreak;
        x[i++] = t.UnbeatenStreak;
        x[i++] = t.HomeGoalsAverage;
        x[i++] = t.AwayGoalsAverage;
        x[i++] = t.DaysSinceLastMatch ?? 0d;
        x[i++] = t.LeaguePosition ?? 0d;
        x[i++] = t.LeaguePoints;
        x[i++] = t.ShotAccuracy ?? 0d;
        x[i++] = t.ShotDifference ?? 0d;
        x[i++] = t.OffensiveRating;
        x[i++] = t.DefensiveRating;
        x[i++] = t.RecentMomentum;
        x[i++] = t.Elo ?? 0d;
    }
}
