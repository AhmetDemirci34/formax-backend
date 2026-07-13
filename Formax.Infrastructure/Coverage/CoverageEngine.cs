using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Coverage.Abstractions;

namespace Formax.Infrastructure.Coverage;

/// <summary>
/// Coverage Engine giriş noktası.
/// Bir maç için TÜM kategorilerin doluluk haritasını üretir: önce her kategori "unknown" olarak
/// başlatılır, sonra kayıtlı analizörler kendi kategorilerini doldurur.
///
/// Bu faz İSKELET: hiçbir kategori analizörü kayıtlı değildir; gerçek Exists/Score/LastUpdated
/// determinasyonu analizörler yazıldığında devreye girer. Şimdilik tüm kategoriler "unknown" döner.
///
/// KAPSAM DIŞI: Eksik veri tamamlama / Provider çağırma / Data Quality / DB burada YOKTUR.
/// </summary>
public sealed class CoverageEngine
{
    private readonly IReadOnlyList<ICoverageAnalyzer> _analyzers;

    public CoverageEngine(IEnumerable<ICoverageAnalyzer> analyzers)
    {
        _analyzers = (analyzers ?? Enumerable.Empty<ICoverageAnalyzer>())
            .Where(a => a is not null)
            .ToList();
    }

    public CoverageResult Analyze(CoverageContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        // Tüm kategoriler varsayılan olarak "unknown/eksik" ile başlatılır → her zaman tam harita.
        var byCategory = new Dictionary<CoverageCategory, CoverageItem>();
        foreach (var category in Enum.GetValues<CoverageCategory>())
            byCategory[category] = CoverageItem.Unknown(category);

        // Analizörler kendi kategorilerini doldurur (iskelet: henüz analizör yok).
        foreach (var analyzer in _analyzers)
        {
            var item = analyzer.Analyze(context);
            if (item is not null)
                byCategory[analyzer.Category] = item;
        }

        var items = byCategory
            .OrderBy(kv => kv.Key)
            .Select(kv => kv.Value)
            .ToList();

        return CoverageResult.From(context.FormaxMatchId, items);
    }
}
