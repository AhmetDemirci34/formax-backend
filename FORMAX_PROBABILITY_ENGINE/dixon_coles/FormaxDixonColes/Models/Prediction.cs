using System.Globalization;

namespace Formax.DixonColes.Models;

public enum Outcome { HomeWin = 0, Draw = 1, AwayWin = 2 }

/// <summary>A probability triple that is guaranteed to be normalised.</summary>
public readonly struct ProbTriple
{
    public double Home { get; }
    public double Draw { get; }
    public double Away { get; }

    public ProbTriple(double home, double draw, double away, double floor)
    {
        home = Math.Max(home, floor);
        draw = Math.Max(draw, floor);
        away = Math.Max(away, floor);
        var s = home + draw + away;
        Home = home / s; Draw = draw / s; Away = away / s;
    }

    public double Sum => Home + Draw + Away;
    public double this[Outcome o] => o switch
    {
        Outcome.HomeWin => Home,
        Outcome.Draw => Draw,
        _ => Away
    };
    public Outcome ArgMax => Home >= Draw && Home >= Away ? Outcome.HomeWin
                           : Draw >= Away ? Outcome.Draw : Outcome.AwayWin;
    public double Max => Math.Max(Home, Math.Max(Draw, Away));
}

public sealed class Prediction
{
    public required string MatchId { get; init; }
    public required DateOnly PredictionDate { get; init; }
    public required string ModelVersion { get; init; }
    public required string TeamStrengthVersion { get; init; }
    public required string ConfigVersion { get; init; }

    public required string Season { get; init; }
    public required string Competition { get; init; }
    public required string CompetitionType { get; init; }
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }

    public required ProbTriple Probabilities { get; init; }
    public double LambdaHome { get; init; }
    public double LambdaAway { get; init; }

    public required Outcome Actual { get; init; }
    public required int HomeGoals { get; init; }
    public required int AwayGoals { get; init; }

    // cold start context, carried through untouched from the team strength snapshots
    public required string HomeColdStartClass { get; init; }
    public required string AwayColdStartClass { get; init; }
    public required string HomeConfidence { get; init; }
    public required string AwayConfidence { get; init; }
    public required double HomePriorWeight { get; init; }
    public required double AwayPriorWeight { get; init; }
    public required string HomePriorSource { get; init; }
    public required string AwayPriorSource { get; init; }
    /// <summary>True when either side has no prior match at all.</summary>
    public required bool IsColdStart { get; init; }
    /// <summary>Latest match date that fed either snapshot. Must be strictly before PredictionDate.</summary>
    public DateOnly? EvidenceCutoff { get; init; }

    private static string N(double v) => v.ToString("0.00000000", CultureInfo.InvariantCulture);

    public static string CsvHeader =>
        "MatchId,PredictionDate,ModelVersion,TeamStrengthVersion,ConfigVersion," +
        "Season,Competition,CompetitionType,HomeTeam,AwayTeam," +
        "HomeProbability,DrawProbability,AwayProbability,ProbabilitySum," +
        "LambdaHome,LambdaAway,ActualOutcome,HomeGoals,AwayGoals," +
        "HomeColdStartClass,AwayColdStartClass,HomeConfidence,AwayConfidence," +
        "HomePriorWeight,AwayPriorWeight,HomePriorSource,AwayPriorSource,IsColdStart,EvidenceCutoff";

    public string ToCsv()
    {
        static string Q(string s) => s.Contains(',') || s.Contains('"')
            ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        return string.Join(',',
            Q(MatchId), PredictionDate.ToString("yyyy-MM-dd"), Q(ModelVersion), Q(TeamStrengthVersion), Q(ConfigVersion),
            Q(Season), Q(Competition), Q(CompetitionType), Q(HomeTeam), Q(AwayTeam),
            N(Probabilities.Home), N(Probabilities.Draw), N(Probabilities.Away), N(Probabilities.Sum),
            N(LambdaHome), N(LambdaAway), Actual.ToString(),
            HomeGoals.ToString(CultureInfo.InvariantCulture), AwayGoals.ToString(CultureInfo.InvariantCulture),
            Q(HomeColdStartClass), Q(AwayColdStartClass), Q(HomeConfidence), Q(AwayConfidence),
            N(HomePriorWeight), N(AwayPriorWeight), Q(HomePriorSource), Q(AwayPriorSource),
            IsColdStart ? "True" : "False",
            EvidenceCutoff?.ToString("yyyy-MM-dd") ?? string.Empty);
    }
}
