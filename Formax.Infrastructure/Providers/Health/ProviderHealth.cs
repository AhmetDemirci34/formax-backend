using System;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Health;

/// <summary>
/// Bir sağlayıcı+yetenek çiftinin sağlık anlık görüntüsü (immutable).
/// Metrikler kaydedilen başarı/başarısızlık olaylarından türer; <see cref="Status"/> ise
/// bu fazda TÜRETİLMEZ (health algoritması yok) — varsayılan <see cref="ProviderHealthStatus.Offline"/>.
/// </summary>
public sealed record ProviderHealth
{
    public required string ProviderName { get; init; }

    public required ProviderCapability Capability { get; init; }

    public ProviderHealthStatus Status { get; init; } = ProviderHealthStatus.Offline;

    public DateTimeOffset? LastSuccess { get; init; }

    public DateTimeOffset? LastFailure { get; init; }

    public int ConsecutiveFailures { get; init; }

    public long TotalRequests { get; init; }

    public long SuccessfulRequests { get; init; }

    public long FailedRequests { get; init; }

    public TimeSpan AverageResponseTime { get; init; }

    public TimeSpan? LastResponseTime { get; init; }

    public string? LastError { get; init; }

    /// <summary>Başarı oranı yüzdesi (0..100). Türetilmiş: başarılı / toplam.</summary>
    public double Availability =>
        TotalRequests == 0 ? 0d : (double)SuccessfulRequests / TotalRequests * 100d;

    /// <summary>Registry indeksleme anahtarı (ad + capability).</summary>
    public string Key => BuildKey(ProviderName, Capability);

    public static string BuildKey(string providerName, ProviderCapability capability) =>
        $"{(providerName ?? string.Empty).Trim().ToLowerInvariant()}::{(int)capability}";

    /// <summary>Henüz olay kaydedilmemiş başlangıç durumu.</summary>
    public static ProviderHealth Initial(string providerName, ProviderCapability capability) => new()
    {
        ProviderName = providerName,
        Capability = capability,
        Status = ProviderHealthStatus.Offline
    };
}
