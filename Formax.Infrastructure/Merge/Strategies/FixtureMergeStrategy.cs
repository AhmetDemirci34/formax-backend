using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Merge.Abstractions;
using Formax.Infrastructure.Merge.Fields;
using Formax.Infrastructure.Normalize.Models;

namespace Formax.Infrastructure.Merge.Strategies;

/// <summary>
/// Aynı FORMAX Match altındaki birden fazla <see cref="NormalizedFixture"/>'ı ALAN BAZLI birleştirir.
///
/// Kurallar:
///  • Boş alan → değeri olan provider'dan doldur (fill missing).
///  • Aynı değer → tek kopya (provenance = ilk sağlayan).
///  • Farklı değer → KARAR VERME: alan undecided (null) bırakılır, adaylar Conflict olarak kaydedilir
///    (veri kaybı yok, çözüm Conflict Engine'e bırakılır).
///  • Eksik kalan alanlar işaretlenir; doldurulan alanlar ve kaynakları provenance'ta tutulur.
/// </summary>
public sealed class FixtureMergeStrategy : IMergeStrategy<NormalizedFixture>
{
    public MergeResult<NormalizedFixture> Merge(
        IReadOnlyList<MergeContribution<NormalizedFixture>> contributions,
        MergeContext context)
    {
        var ordered = (contributions ?? Array.Empty<MergeContribution<NormalizedFixture>>())
            .Where(c => c is not null && c.Data is not null)
            .OrderByDescending(c => c.Priority)
            .ToList();

        var tracker = new MergeFieldTracker();

        var merged = new NormalizedFixture
        {
            ProviderMatchId = MergeField("ProviderMatchId", ordered, f => f.ProviderMatchId, tracker),
            HomeTeam = MergeField("HomeTeam", ordered, f => f.HomeTeam, tracker),
            AwayTeam = MergeField("AwayTeam", ordered, f => f.AwayTeam, tracker),
            Competition = MergeField("Competition", ordered, f => f.Competition, tracker),
            Season = MergeField("Season", ordered, f => f.Season, tracker),
            Round = MergeField("Round", ordered, f => f.Round, tracker),
            Venue = MergeField("Venue", ordered, f => f.Venue, tracker),
            KickoffUtc = MergeField("KickoffUtc", ordered, f => f.KickoffUtc, tracker),
            Status = MergeField("Status", ordered,
                        f => f.Status == FixtureStatus.Unknown ? (FixtureStatus?)null : f.Status, tracker)
                     ?? FixtureStatus.Unknown,
            HomeScore = MergeField("HomeScore", ordered, f => f.HomeScore, tracker),
            AwayScore = MergeField("AwayScore", ordered, f => f.AwayScore, tracker)
        };

        return MergeResult<NormalizedFixture>.From(merged, tracker);
    }

    /// <summary>
    /// Tek alan için birleştirme: değeri olanları toplar; hiç yoksa eksik işaretler; hepsi aynıysa tek
    /// kopya döndürür (provenance kaydeder); farklıysa KARAR VERMEDEN çatışma kaydeder ve null döndürür.
    /// </summary>
    private static TField MergeField<TField>(
        string fieldName,
        IReadOnlyList<MergeContribution<NormalizedFixture>> contributions,
        Func<NormalizedFixture, TField> selector,
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
            // State = Missing (null ≠ Missing: alanın neden boş olduğu açıkça izlenir).
            tracker.RecordMissing(fieldName);
            return default!;
        }

        var comparer = EqualityComparer<TField>.Default;
        var firstValue = present[0].Value;
        var allSame = present.All(p => comparer.Equals(p.Value, firstValue));

        if (allSame)
        {
            // Eksik doldurma veya aynı değer → tek kopya; kaynak = değeri veren ilk provider. State = Merged.
            tracker.RecordMerged(fieldName, present[0].Provider, firstValue);
            return firstValue;
        }

        // Farklı değerler → karar verme; adayları koru (veri kaybı yok), Conflict Engine'e bırak. State = Conflict.
        var candidates = present
            .Select(p => new MergeFieldCandidate { ProviderName = p.Provider, Value = p.Value })
            .ToList();

        tracker.RecordConflict(fieldName, candidates);
        return default!;
    }
}
