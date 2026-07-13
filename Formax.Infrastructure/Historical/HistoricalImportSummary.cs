namespace Formax.Infrastructure.Historical;

/// <summary>Tarihsel import sonucunun özeti (gerçek sayımlarla).</summary>
public sealed record HistoricalImportSummary
{
    public int MatchesRead { get; init; }
    public int MatchesImported { get; init; }
    public int MatchesDuplicate { get; init; }
    public int MatchesSkipped { get; init; }

    public int EloRead { get; init; }
    public int EloImported { get; init; }
    public int EloDuplicate { get; init; }
    public int EloMatchedToTeam { get; init; }

    public int CompetitionsCreated { get; init; }
    public int TeamsCreated { get; init; }

    public long ElapsedMs { get; init; }
    public long PeakMemoryMB { get; init; }
}
