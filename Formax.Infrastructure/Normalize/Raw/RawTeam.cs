namespace Formax.Infrastructure.Normalize.Raw;

/// <summary>Provider-bağımsız ortak HAM takım modeli. Tüm provider mapper'ları buna üretir.</summary>
public sealed record RawTeam
{
    public string? ProviderName { get; init; }

    public string? Name { get; init; }

    public string? ShortName { get; init; }

    public string? Country { get; init; }
}
