using System;

namespace Formax.Infrastructure.Normalize.Models;

/// <summary>
/// Ortak fikstür/maç modeli. Zaman her zaman UTC olarak taşınır; durum ortak
/// <see cref="FixtureStatus"/> modeline normalize edilir. Eksik alanlar null bırakılır.
/// </summary>
public sealed record NormalizedFixture
{
    public string? ProviderMatchId { get; init; }

    public NormalizedTeam? HomeTeam { get; init; }

    public NormalizedTeam? AwayTeam { get; init; }

    public NormalizedCompetition? Competition { get; init; }

    public string? Season { get; init; }

    public string? Round { get; init; }

    public NormalizedVenue? Venue { get; init; }

    /// <summary>Başlama zamanı (UTC).</summary>
    public DateTimeOffset? KickoffUtc { get; init; }

    /// <summary>Ortak durum modeli.</summary>
    public FixtureStatus Status { get; init; } = FixtureStatus.Unknown;

    public int? HomeScore { get; init; }

    public int? AwayScore { get; init; }
}
