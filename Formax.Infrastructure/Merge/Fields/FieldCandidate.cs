namespace Formax.Infrastructure.Merge.Fields;

/// <summary>
/// Bir alanın tek bir provider'daki adayı (değeri var/yok bilgisiyle).
/// Alan-bazlı merge sırasında adayları incelemek için kullanılır; hiçbir seçim/karar içermez.
/// </summary>
public sealed record FieldCandidate<TValue>
{
    public required string ProviderName { get; init; }

    public TValue? Value { get; init; }

    public bool HasValue { get; init; }
}
