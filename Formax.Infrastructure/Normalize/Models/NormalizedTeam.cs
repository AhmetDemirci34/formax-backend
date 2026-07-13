namespace Formax.Infrastructure.Normalize.Models;

/// <summary>Ortak takım modeli. Alanlar ihtiyaç arttıkça genişletilebilir.</summary>
public sealed record NormalizedTeam
{
    public required string Name { get; init; }

    public string? ShortName { get; init; }

    public NormalizedCountry? Country { get; init; }
}
