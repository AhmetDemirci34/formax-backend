using System;

namespace Formax.Infrastructure.Conflict;

/// <summary>
/// Bir alan için tek bir provider'dan gelen aday değer ve karar kriterleri.
/// Bilerek provider-bağımsızdır: yalnızca provider adı + primitive metadata taşır.
/// Kriterler, ileride çözüm stratejilerinin kullanacağı sinyallerdir.
/// </summary>
public sealed record ConflictCandidate<T>
{
    public required string ProviderName { get; init; }

    public required T Value { get; init; }

    /// <summary>Provider önceliği (Provider Priority kriteri).</summary>
    public int ProviderPriority { get; init; }

    /// <summary>Provider'ın bu değere güveni 0..1 (Provider Confidence kriteri).</summary>
    public double? ProviderConfidence { get; init; }

    /// <summary>Değerin son güncellenme zamanı, UTC (Last Updated kriteri).</summary>
    public DateTimeOffset? LastUpdatedUtc { get; init; }

    /// <summary>Bu değeri destekleyen kaynak sayısı (Source Count kriteri).</summary>
    public int SourceCount { get; init; } = 1;

    /// <summary>Manuel geçersiz kılma işareti (Manual Override kriteri).</summary>
    public bool IsManualOverride { get; init; }
}
