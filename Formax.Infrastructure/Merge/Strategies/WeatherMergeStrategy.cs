using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Merge.Abstractions;
using Formax.Infrastructure.Merge.Fields;
using Formax.Infrastructure.Normalize.Models;

namespace Formax.Infrastructure.Merge.Strategies;

/// <summary>
/// Aynı koordinata ait birden fazla <see cref="NormalizedWeather"/>'ı (Open-Meteo, MET Norway, …) ALAN BAZLI birleştirir.
///
/// Kurallar <see cref="FixtureMergeStrategy"/> ile aynıdır:
///  • Boş alan → değeri olan provider'dan doldur (fill missing).
///  • Aynı değer → tek kopya (provenance = ilk sağlayan).
///  • Farklı değer → KARAR VERME: alan null bırakılır, adaylar Conflict olarak kaydedilir
///    (veri kaybı yok; çözüm Conflict Engine'e bırakılır — Weather'da LastUpdated → ProviderPriority).
/// </summary>
public sealed class WeatherMergeStrategy : IMergeStrategy<NormalizedWeather>
{
    public MergeResult<NormalizedWeather> Merge(
        IReadOnlyList<MergeContribution<NormalizedWeather>> contributions,
        MergeContext context)
    {
        var ordered = (contributions ?? Array.Empty<MergeContribution<NormalizedWeather>>())
            .Where(c => c is not null && c.Data is not null)
            .OrderByDescending(c => c.Priority)
            .ToList();

        var tracker = new MergeFieldTracker();

        var merged = new NormalizedWeather
        {
            Latitude = MergeField("Latitude", ordered, w => w.Latitude, tracker),
            Longitude = MergeField("Longitude", ordered, w => w.Longitude, tracker),
            TemperatureC = MergeField("TemperatureC", ordered, w => w.TemperatureC, tracker),
            Condition = MergeField("Condition", ordered, w => w.Condition, tracker),
            ForecastUtc = MergeField("ForecastUtc", ordered, w => w.ForecastUtc, tracker)
        };

        return MergeResult<NormalizedWeather>.From(merged, tracker);
    }

    private static TField MergeField<TField>(
        string fieldName,
        IReadOnlyList<MergeContribution<NormalizedWeather>> contributions,
        Func<NormalizedWeather, TField> selector,
        MergeFieldTracker tracker)
    {
        var present = new List<(string Provider, TField Value)>();
        foreach (var contribution in contributions)
        {
            var value = selector(contribution.Data);
            if (value is not null)
                present.Add((contribution.ProviderName, value));
        }

        if (present.Count == 0)
        {
            tracker.RecordMissing(fieldName);
            return default!;
        }

        var comparer = EqualityComparer<TField>.Default;
        var firstValue = present[0].Value;
        var allSame = present.All(p => comparer.Equals(p.Value, firstValue));

        if (allSame)
        {
            tracker.RecordMerged(fieldName, present[0].Provider, firstValue);
            return firstValue;
        }

        var candidates = present
            .Select(p => new MergeFieldCandidate { ProviderName = p.Provider, Value = p.Value })
            .ToList();

        tracker.RecordConflict(fieldName, candidates);
        return default!;
    }
}
