namespace Formax.Infrastructure.Normalize.Raw;

/// <summary>Provider-bağımsız ortak HAM lig/turnuva modeli. Tüm provider mapper'ları buna üretir.</summary>
public sealed record RawCompetition
{
    public string? ProviderName { get; init; }

    public string? Name { get; init; }

    public string? Country { get; init; }

    public string? Season { get; init; }
}
