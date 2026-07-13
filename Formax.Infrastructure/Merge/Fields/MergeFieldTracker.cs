using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Infrastructure.Merge.Fields;

/// <summary>
/// Merge sırasında her alanın DURUMUNU (Merged / Missing / Conflict) ve provenance'ını biriktiren yardımcı.
/// Saf mekanizmadır: durumu strateji söyler; kendisi karar vermez.
/// </summary>
public sealed class MergeFieldTracker
{
    private readonly List<MergeFieldOutcome> _fields = new();
    private readonly HashSet<string> _contributors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Bir alanın bir provider'dan başarıyla dolduğunu kaydeder (State = Merged).</summary>
    public void RecordMerged(string field, string provider, object? value)
    {
        if (string.IsNullOrWhiteSpace(field)) throw new ArgumentException("Alan adı boş olamaz.", nameof(field));
        if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("Provider adı boş olamaz.", nameof(provider));

        _fields.Add(new MergeFieldOutcome
        {
            Field = field,
            Value = value,
            State = MergeFieldState.Merged,
            Provider = provider,
            Provenance = new[] { new MergeFieldCandidate { ProviderName = provider, Value = value } }
        });

        _contributors.Add(provider);
    }

    /// <summary>Bir alanı hiçbir provider'ın sağlamadığını kaydeder (State = Missing).</summary>
    public void RecordMissing(string field)
    {
        if (string.IsNullOrWhiteSpace(field)) throw new ArgumentException("Alan adı boş olamaz.", nameof(field));

        _fields.Add(new MergeFieldOutcome
        {
            Field = field,
            Value = null,
            State = MergeFieldState.Missing
        });
    }

    /// <summary>
    /// Bir alan için farklı provider değerleri (çatışma) olduğunu KARAR VERMEDEN kaydeder (State = Conflict).
    /// Adaylar korunur (veri kaybı yok); çözüm Conflict Engine'e bırakılır.
    /// </summary>
    public void RecordConflict(string field, IReadOnlyList<MergeFieldCandidate> candidates)
    {
        if (string.IsNullOrWhiteSpace(field)) throw new ArgumentException("Alan adı boş olamaz.", nameof(field));
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));

        _fields.Add(new MergeFieldOutcome
        {
            Field = field,
            Value = null,
            State = MergeFieldState.Conflict,
            Provenance = candidates
        });

        foreach (var candidate in candidates)
        {
            if (candidate is not null && !string.IsNullOrWhiteSpace(candidate.ProviderName))
                _contributors.Add(candidate.ProviderName);
        }
    }

    /// <summary>Alan-başına sonuçlar (değer + durum + kaynak).</summary>
    public IReadOnlyList<MergeFieldOutcome> Fields => _fields;

    /// <summary>Sonuca en az bir alan sağlamış provider'lar.</summary>
    public IReadOnlyList<string> Contributors => _contributors.ToList();
}
