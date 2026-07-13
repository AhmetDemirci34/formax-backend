namespace Formax.Infrastructure.Normalize.Raw;

/// <summary>Provider-bağımsız ortak HAM stat/mekan modeli. Tüm provider mapper'ları buna üretir.</summary>
public sealed record RawVenue
{
    public string? ProviderName { get; init; }

    public string? Name { get; init; }

    public string? City { get; init; }

    public string? Country { get; init; }

    /// <summary>Ham kapasite metni; sayıya dönüşüm Normalize Engine'de yapılır.</summary>
    public string? Capacity { get; init; }
}
