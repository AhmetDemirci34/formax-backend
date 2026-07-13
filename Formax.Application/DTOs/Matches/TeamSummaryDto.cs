namespace Formax.Application.DTOs.Matches;

public class TeamSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? LogoUrl { get; set; }

    /// <summary>League standing position. Null when standings are unavailable.</summary>
    public int? Rank { get; set; }
}
