using System;

namespace Formax.Infrastructure.Coverage;

/// <summary>
/// Tek bir veri kategorisinin doluluk durumu.
/// </summary>
public sealed record CoverageItem
{
    public required CoverageCategory Category { get; init; }

    /// <summary>Bu kategori için veri mevcut mu?</summary>
    public bool Exists { get; init; }

    /// <summary>Türetilmiş: veri eksik mi? (<see cref="Exists"/>'in tersi)</summary>
    public bool Missing => !Exists;

    /// <summary>Kategori doluluk skoru, 0..1.</summary>
    public double CoverageScore { get; init; }

    /// <summary>Bu kategorinin en son güncellendiği zaman (UTC).</summary>
    public DateTimeOffset? LastUpdated { get; init; }

    /// <summary>Verinin bilinmediği/eksik olduğu varsayılan durum.</summary>
    public static CoverageItem Unknown(CoverageCategory category) => new()
    {
        Category = category,
        Exists = false,
        CoverageScore = 0d,
        LastUpdated = null
    };

    /// <summary>Verinin mevcut olduğu durum (analizörler tarafından üretilecek).</summary>
    public static CoverageItem Present(CoverageCategory category, double score, DateTimeOffset? lastUpdated = null) => new()
    {
        Category = category,
        Exists = true,
        CoverageScore = score,
        LastUpdated = lastUpdated
    };
}
