using System;
using System.Collections.Generic;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Configuration;

/// <summary>
/// Bir sağlayıcı+yetenek çifti için merkezi yapılandırma.
/// BaseUrl, Timeout, Retry, ApiKey, Priority gibi ayarlar artık provider sınıflarında DEĞİL,
/// burada merkezî olarak tutulur. Salt-okunur; ileride IConfiguration/appsettings/yönetim
/// panelinden beslenebilir (gerçek yükleme bu fazda YOK).
/// </summary>
public sealed record ProviderConfiguration
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>();

    private static readonly IReadOnlyDictionary<string, string?> EmptyQuery =
        new Dictionary<string, string?>();

    public required string ProviderName { get; init; }

    public required ProviderCapability Capability { get; init; }

    public bool Enabled { get; init; } = true;

    public int Priority { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public int RetryCount { get; init; }

    public int MaxParallelism { get; init; } = 1;

    /// <summary>İstek/dakika üst sınırı (null = sınırsız).</summary>
    public int? RateLimit { get; init; }

    /// <summary>Günlük istek üst sınırı (null = sınırsız).</summary>
    public int? DailyLimit { get; init; }

    /// <summary>İsteğe eklenecek HTTP başlıkları.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = EmptyHeaders;

    /// <summary>İsteğe eklenecek sorgu parametreleri.</summary>
    public IReadOnlyDictionary<string, string?> QueryParameters { get; init; } = EmptyQuery;

    public string? BaseUrl { get; init; }

    /// <summary>Opsiyonel API anahtarı (ücretsiz kaynaklar için null).</summary>
    public string? ApiKey { get; init; }

    public string? Notes { get; init; }

    /// <summary>Registry indeksleme anahtarı (ad + capability).</summary>
    public string Key => BuildKey(ProviderName, Capability);

    public static string BuildKey(string providerName, ProviderCapability capability) =>
        $"{(providerName ?? string.Empty).Trim().ToLowerInvariant()}::{(int)capability}";

    /// <summary>Yapılandırma oluşturmak için ergonomik fabrika.</summary>
    public static ProviderConfiguration Create(
        string providerName,
        ProviderCapability capability,
        bool enabled = true,
        int priority = 0,
        TimeSpan? timeout = null,
        int retryCount = 0,
        int maxParallelism = 1,
        int? rateLimit = null,
        int? dailyLimit = null,
        IReadOnlyDictionary<string, string>? headers = null,
        IReadOnlyDictionary<string, string?>? queryParameters = null,
        string? baseUrl = null,
        string? apiKey = null,
        string? notes = null) => new()
        {
            ProviderName = providerName,
            Capability = capability,
            Enabled = enabled,
            Priority = priority,
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
            RetryCount = retryCount < 0 ? 0 : retryCount,
            MaxParallelism = maxParallelism < 1 ? 1 : maxParallelism,
            RateLimit = rateLimit,
            DailyLimit = dailyLimit,
            Headers = headers ?? EmptyHeaders,
            QueryParameters = queryParameters ?? EmptyQuery,
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            Notes = notes
        };
}
