namespace Formax.Infrastructure.Normalize.Models;

/// <summary>Ortak oyuncu modeli.</summary>
public sealed record NormalizedPlayer
{
    public required string FullName { get; init; }

    public string? Position { get; init; }

    public NormalizedCountry? Nationality { get; init; }
}
