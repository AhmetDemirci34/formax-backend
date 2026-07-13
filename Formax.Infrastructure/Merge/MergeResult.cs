using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Conflict;
using Formax.Infrastructure.Merge.Fields;

namespace Formax.Infrastructure.Merge;

/// <summary>
/// Birleştirme sonucu: tek birleşmiş <typeparamref name="T"/> değeri, ALAN-BAŞINA durum/provenance bilgisi
/// ve (Conflict aşaması işlediğinde) çatışma çözümleri. Conflict, Merge'in devamıdır; ayrı bir sonuç modeli
/// yoktur — bu model zenginleşir.
/// </summary>
public sealed record MergeResult<T>
{
    public required T Value { get; init; }

    /// <summary>Her merge edilen alanın değeri, durumu, kaynağı ve provenance'ı.</summary>
    public IReadOnlyList<MergeFieldOutcome> Fields { get; init; } = Array.Empty<MergeFieldOutcome>();

    /// <summary>Sonuca en az bir alan sağlamış provider'lar.</summary>
    public IReadOnlyList<string> Contributors { get; init; } = Array.Empty<string>();

    /// <summary>Conflict aşamasının ürettiği alan-çözümleri (aşama çalışmadıysa boş).</summary>
    public IReadOnlyList<ConflictResolution> ConflictResolutions { get; init; } = Array.Empty<ConflictResolution>();

    // ----- Durum-bazlı türetilmiş görünümler (işaretleme) -----

    /// <summary>Bir provider'dan doldurulan alan adları.</summary>
    public IEnumerable<string> MergedFields => Fields.Where(f => f.State == MergeFieldState.Merged).Select(f => f.Field);

    /// <summary>Hiçbir provider'ın sağlamadığı, eksik kalan alan adları.</summary>
    public IEnumerable<string> MissingFields => Fields.Where(f => f.State == MergeFieldState.Missing).Select(f => f.Field);

    /// <summary>Farklı değerler yüzünden karar verilmeyen (Conflict Engine'e bırakılan) alan adları.</summary>
    public IEnumerable<string> ConflictedFields => Fields.Where(f => f.State == MergeFieldState.Conflict).Select(f => f.Field);

    /// <summary>Birleşmiş değeri ve tracker'da biriken alan-durumlarını tek sonuca toplar.</summary>
    public static MergeResult<T> From(T value, MergeFieldTracker tracker)
    {
        if (tracker is null) throw new ArgumentNullException(nameof(tracker));

        return new MergeResult<T>
        {
            Value = value,
            Fields = tracker.Fields,
            Contributors = tracker.Contributors
        };
    }
}
