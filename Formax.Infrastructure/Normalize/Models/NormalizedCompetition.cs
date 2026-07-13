namespace Formax.Infrastructure.Normalize.Models;

/// <summary>Ortak lig/turnuva modeli. Kanonik lig eşlemesi sonraki fazda genişletilecek.</summary>
public sealed record NormalizedCompetition
{
    public required string Name { get; init; }

    public NormalizedCountry? Country { get; init; }

    /// <summary>Sezon etiketi (ör. "2025/2026"). Opsiyonel.</summary>
    public string? Season { get; init; }
}
