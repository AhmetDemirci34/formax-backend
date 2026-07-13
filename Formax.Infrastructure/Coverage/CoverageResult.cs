using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Infrastructure.Coverage;

/// <summary>
/// Bir maçın tüm kategoriler üzerindeki doluluk haritası.
/// </summary>
public sealed record CoverageResult
{
    public string? FormaxMatchId { get; init; }

    /// <summary>Her kategori için doluluk durumu (kategori sırasına göre).</summary>
    public required IReadOnlyList<CoverageItem> Items { get; init; }

    /// <summary>Item skorlarının özeti (0..1). Not: bu yalnızca verilen item'ları özetler,
    /// kategori-düzeyi gerçek hesaplama analizörlerin işidir.</summary>
    public double OverallScore { get; init; }

    public IReadOnlyList<CoverageCategory> PresentCategories { get; init; } = Array.Empty<CoverageCategory>();

    public IReadOnlyList<CoverageCategory> MissingCategories { get; init; } = Array.Empty<CoverageCategory>();

    /// <summary>Analizörlerce üretilmiş item'ları tek sonuca özetler (kategori determinasyonu yapmaz).</summary>
    public static CoverageResult From(string? formaxMatchId, IReadOnlyList<CoverageItem> items)
    {
        items ??= Array.Empty<CoverageItem>();

        return new CoverageResult
        {
            FormaxMatchId = formaxMatchId,
            Items = items,
            OverallScore = items.Count == 0 ? 0d : items.Average(i => i.CoverageScore),
            PresentCategories = items.Where(i => i.Exists).Select(i => i.Category).ToList(),
            MissingCategories = items.Where(i => i.Missing).Select(i => i.Category).ToList()
        };
    }
}
