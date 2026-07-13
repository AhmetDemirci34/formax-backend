namespace Formax.Infrastructure.Normalize.Raw;

/// <summary>Provider-bağımsız ortak HAM oyuncu modeli. Tüm provider mapper'ları buna üretir.</summary>
public sealed record RawPlayer
{
    public string? ProviderName { get; init; }

    public string? FullName { get; init; }

    public string? Position { get; init; }

    public string? Nationality { get; init; }
}
