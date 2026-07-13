namespace Formax.Infrastructure.Merge;

/// <summary>
/// Merge edilmiş bir alanın durumu. null ≠ Missing: bir alan boşsa NEDEN boş olduğu buradan görülür.
/// </summary>
public enum MergeFieldState
{
    /// <summary>Değer bir provider'dan başarıyla dolduruldu (fill missing veya aynı değer).</summary>
    Merged = 0,

    /// <summary>Hiçbir provider bu alanı sağlamadı → değer yok.</summary>
    Missing = 1,

    /// <summary>Provider'lar farklı değerler verdi → karar verilmedi (Conflict Engine'e bırakıldı).</summary>
    Conflict = 2
}
