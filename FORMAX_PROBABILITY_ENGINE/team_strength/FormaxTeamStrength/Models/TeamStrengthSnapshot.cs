using System.Globalization;

namespace Formax.TeamStrength.Models;

public enum ColdStartClass
{
    /// <summary>No prior match at all: the snapshot is the prior, nothing else.</summary>
    NoHistory,
    /// <summary>1-2 effective prior matches.</summary>
    Limited,
    /// <summary>3-4 effective prior matches.</summary>
    Developing,
    /// <summary>5-9 effective prior matches.</summary>
    Established,
    /// <summary>10+ effective prior matches.</summary>
    Rich
}

public enum ConfidenceLevel { None, Low, MediumLow, Medium, High }

/// <summary>
/// Pre-match state of one team, valid strictly BEFORE the match it is attached to.
/// Every value is derived only from matches played earlier than <see cref="MatchDate"/>.
/// </summary>
public sealed class TeamStrengthSnapshot
{
    public required string MatchId { get; init; }
    public required string TeamId { get; init; }
    public required string TeamName { get; init; }
    public required DateOnly MatchDate { get; init; }
    public required string Side { get; init; }              // HOME | AWAY
    public required string CompetitionType { get; init; }
    public required string Competition { get; init; }

    /// <summary>Multiplicative attack index. 1.0 = pool average. Higher scores more.</summary>
    public required double AttackStrength { get; init; }
    /// <summary>Multiplicative concession index. 1.0 = pool average. LOWER concedes less.</summary>
    public required double DefenseStrength { get; init; }
    /// <summary>Attack / Defense. Higher is stronger.</summary>
    public required double OverallStrength { get; init; }
    /// <summary>Venue specific overall index built from home matches only. Null when no home history.</summary>
    public double? HomeStrength { get; init; }
    /// <summary>Venue specific overall index built from away matches only. Null when no away history.</summary>
    public double? AwayStrength { get; init; }

    /// <summary>Raw count of prior matches that fed this snapshot.</summary>
    public required int MatchesUsed { get; init; }
    /// <summary>Time-weighted count of prior matches (decayed). Drives shrinkage.</summary>
    public required double EffectiveMatches { get; init; }
    public required int HomeMatchesUsed { get; init; }
    public required int AwayMatchesUsed { get; init; }

    public required ConfidenceLevel Confidence { get; init; }
    public required ColdStartClass ColdStartClass { get; init; }

    /// <summary>Which pool the shrinkage pulled towards. Recorded for every snapshot, always.</summary>
    public required string PriorSource { get; init; }
    /// <summary>Weight the pool received (0 = pure own evidence, 1 = pure prior).</summary>
    public required double PriorWeight { get; init; }

    /// <summary>Date of the most recent match that fed this snapshot. Null when there is none.</summary>
    public DateOnly? LastMatchDate { get; init; }

    private static string N(double v) => v.ToString("0.000000", CultureInfo.InvariantCulture);
    private static string N(double? v) => v.HasValue ? N(v.Value) : string.Empty;

    public static string CsvHeader =>
        "MatchId,TeamId,TeamName,MatchDate,Side,Competition,CompetitionType," +
        "AttackStrength,DefenseStrength,OverallStrength,HomeStrength,AwayStrength," +
        "MatchesUsed,EffectiveMatches,HomeMatchesUsed,AwayMatchesUsed," +
        "Confidence,ColdStartClass,PriorSource,PriorWeight,LastMatchDate";

    public string ToCsv()
    {
        static string Q(string s) => s.Contains(',') || s.Contains('"')
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;

        return string.Join(',',
            Q(MatchId), Q(TeamId), Q(TeamName), MatchDate.ToString("yyyy-MM-dd"), Side,
            Q(Competition), Q(CompetitionType),
            N(AttackStrength), N(DefenseStrength), N(OverallStrength), N(HomeStrength), N(AwayStrength),
            MatchesUsed.ToString(CultureInfo.InvariantCulture),
            N(EffectiveMatches),
            HomeMatchesUsed.ToString(CultureInfo.InvariantCulture),
            AwayMatchesUsed.ToString(CultureInfo.InvariantCulture),
            Confidence.ToString(), ColdStartClass.ToString(), Q(PriorSource), N(PriorWeight),
            LastMatchDate?.ToString("yyyy-MM-dd") ?? string.Empty);
    }
}
