namespace Formax.TeamStrength.Models;

/// <summary>
/// One historical match, exactly as it exists in FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv.
/// Nothing here is invented: every field is read from the dataset.
/// </summary>
public sealed class MatchRecord
{
    public required string MatchId { get; init; }
    public required DateOnly Date { get; init; }
    public required string Season { get; init; }
    public required string Competition { get; init; }
    public required string CompetitionType { get; init; }

    public required string HomeTeamId { get; init; }
    public required string AwayTeamId { get; init; }
    public required string HomeTeamName { get; init; }
    public required string AwayTeamName { get; init; }

    /// <summary>Final score, extra time included, shootout excluded (master contract).</summary>
    public required int HomeGoals { get; init; }
    public required int AwayGoals { get; init; }

    public required string MatchStatus { get; init; }
    public required string IdentityConfidence { get; init; }

    public override string ToString() =>
        $"{Date:yyyy-MM-dd} {Competition} {HomeTeamName} {HomeGoals}-{AwayGoals} {AwayTeamName}";
}
