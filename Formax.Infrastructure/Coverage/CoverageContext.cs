namespace Formax.Infrastructure.Coverage;

/// <summary>
/// Coverage analizinin girdisi.
/// FORMAX Match ID string olarak taşınır (önceki engine'lere kuplaj yok).
/// <see cref="Source"/>, analizörlerin inceleyeceği veriye açık (opaque) genişleme kancasıdır.
/// </summary>
public sealed record CoverageContext
{
    public string? FormaxMatchId { get; init; }

    /// <summary>Analizörlerin ileride inceleyeceği maç verisi (tür-bağımsız; şimdilik opaque).</summary>
    public object? Source { get; init; }
}
