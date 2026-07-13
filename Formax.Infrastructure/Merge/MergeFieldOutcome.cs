using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.Merge;

/// <summary>
/// Tek bir merge edilmiş alanın tam sonucu: değeri, DURUMU, kaynağı ve provenance'ı.
/// Bir alanın neden boş olduğu (Missing mi Conflict mi) burada açıkça görülür.
/// </summary>
public sealed record MergeFieldOutcome
{
    /// <summary>Alan adı (ör. "Venue").</summary>
    public required string Field { get; init; }

    /// <summary>Merge edilmiş değer. Missing/Conflict durumunda null.</summary>
    public object? Value { get; init; }

    /// <summary>Alanın durumu (Merged / Missing / Conflict).</summary>
    public required MergeFieldState State { get; init; }

    /// <summary>Merged durumunda değeri sağlayan provider; diğer durumlarda null.</summary>
    public string? Provider { get; init; }

    /// <summary>
    /// Alanın kaynak izi: Merged'de tek aday (kazanan), Conflict'te tüm farklı adaylar
    /// (veri kaybı yok), Missing'de boş.
    /// </summary>
    public IReadOnlyList<MergeFieldCandidate> Provenance { get; init; } = Array.Empty<MergeFieldCandidate>();
}

/// <summary>Bir alan için tek bir provider'ın aday değeri (provenance / conflict adayı).</summary>
public sealed record MergeFieldCandidate
{
    public required string ProviderName { get; init; }

    public object? Value { get; init; }
}
