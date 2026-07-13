namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>
/// Tek bir sağlayıcı çağrısının nötr sonucu.
/// <see cref="Payload"/> ham (un-normalized) veridir; tipleme, birleştirme ve
/// çakışma çözümü sonraki Normalize / Merge / Conflict katmanlarının işidir.
/// Bu iskelet bilerek ham <see cref="object"/> taşır.
/// </summary>
public sealed class ProviderResult
{
    public required string ProviderName { get; init; }

    public bool Success { get; init; }

    /// <summary>Ham sağlayıcı çıktısı. Normalize katmanı tarafından tiplenecek.</summary>
    public object? Payload { get; init; }

    public string? Error { get; init; }

    public static ProviderResult Ok(string providerName, object? payload) => new()
    {
        ProviderName = providerName,
        Success = true,
        Payload = payload
    };

    public static ProviderResult Fail(string providerName, string error) => new()
    {
        ProviderName = providerName,
        Success = false,
        Error = error
    };
}
