namespace Formax.Infrastructure.Normalize.Models;

/// <summary>Ortak stat/mekan modeli.</summary>
public sealed record NormalizedVenue
{
    public required string Name { get; init; }

    public string? City { get; init; }

    public NormalizedCountry? Country { get; init; }

    public int? Capacity { get; init; }
}
