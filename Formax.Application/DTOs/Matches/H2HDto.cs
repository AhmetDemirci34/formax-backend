namespace Formax.Application.DTOs.Matches;

public sealed class H2HDto
{
    public int TotalMatches { get; init; }
    public int HomeWins { get; init; }
    public int AwayWins { get; init; }
    public int Draws { get; init; }
    public List<H2HMatchDto> Matches { get; init; } = new();
    public DateTime FetchedAt { get; init; }
}

public sealed class H2HMatchDto
{
    public string MatchDate { get; init; } = "";
    public string HomeTeamName { get; init; } = "";
    public string AwayTeamName { get; init; } = "";
    public int HomeScore { get; init; }
    public int AwayScore { get; init; }
    public string? Competition { get; init; }
}
