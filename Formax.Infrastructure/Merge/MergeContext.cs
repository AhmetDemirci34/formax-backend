namespace Formax.Infrastructure.Merge;

/// <summary>
/// Bir merge işleminin bağlamı.
/// FORMAX Match ID string olarak taşınır (Match Identity Engine'e kuplaj yok).
/// </summary>
public sealed record MergeContext
{
    /// <summary>Katkıların ait olduğu FORMAX Match ID (izlenebilirlik için, opsiyonel).</summary>
    public string? FormaxMatchId { get; init; }
}
