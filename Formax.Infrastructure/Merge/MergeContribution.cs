namespace Formax.Infrastructure.Merge;

/// <summary>
/// Tek bir provider'ın, birleştirilecek maça yaptığı katkı (o provider'ın gördüğü veri).
/// Bilerek provider-bağımsızdır: <typeparamref name="T"/> çağıran tarafça verilir.
/// </summary>
public sealed record MergeContribution<T>
{
    /// <summary>Katkıyı sağlayan provider adı (provenance için).</summary>
    public required string ProviderName { get; init; }

    /// <summary>Provider'ın bu maça dair verisi.</summary>
    public required T Data { get; init; }

    /// <summary>Provider önceliği (ileride alan seçiminde/eşitlik bozmada kullanılabilir).</summary>
    public int Priority { get; init; }
}
