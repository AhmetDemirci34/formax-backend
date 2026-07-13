using System;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Scheduling;

/// <summary>
/// Bir sağlayıcının belirli bir yetenek için merkezi zamanlama tanımı.
/// Yenileme sıklığı ve çalıştırma politikası artık provider/manifest'te DEĞİL, burada tutulur.
/// Bir sağlayıcının her yeteneği farklı bir zamanlamaya sahip olabilir (ör. Live 30sn, News 10dk).
/// </summary>
public sealed record ProviderSchedule
{
    /// <summary>Zamanlanan sağlayıcının adı (manifest adı ile eşleşir).</summary>
    public required string ProviderName { get; init; }

    /// <summary>Bu zamanlamanın ait olduğu veri türü/yetenek.</summary>
    public required ProviderCapability Capability { get; init; }

    /// <summary>Zamanlama etkin mi? (devre dışıysa çalıştırılmaz)</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Yenileme aralığı. <see cref="TimeSpan.Zero"/> = talep üzerine / planlı yenileme yok.</summary>
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.Zero;

    /// <summary>Yüksek değer önce çalıştırılır.</summary>
    public int Priority { get; init; }

    /// <summary>Başarısızlıkta yeniden deneme sayısı.</summary>
    public int RetryCount { get; init; }

    /// <summary>Tek çalıştırma için zaman aşımı.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Bu zamanlama için eşzamanlı azami çalıştırma sayısı.</summary>
    public int MaxParallelism { get; init; } = 1;

    /// <summary>Registry indeksleme anahtarı (ad + capability).</summary>
    public string Key => BuildKey(ProviderName, Capability);

    /// <summary>Ad (büyük/küçük harf duyarsız) + capability'den kararlı anahtar üretir.</summary>
    public static string BuildKey(string providerName, ProviderCapability capability) =>
        $"{(providerName ?? string.Empty).Trim().ToLowerInvariant()}::{(int)capability}";

    /// <summary>Zamanlama oluşturmak için ergonomik fabrika.</summary>
    public static ProviderSchedule Create(
        string providerName,
        ProviderCapability capability,
        TimeSpan refreshInterval,
        bool enabled = true,
        int priority = 0,
        int retryCount = 0,
        TimeSpan? timeout = null,
        int maxParallelism = 1) => new()
        {
            ProviderName = providerName,
            Capability = capability,
            Enabled = enabled,
            RefreshInterval = refreshInterval,
            Priority = priority,
            RetryCount = retryCount < 0 ? 0 : retryCount,
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
            MaxParallelism = maxParallelism < 1 ? 1 : maxParallelism
        };
}
