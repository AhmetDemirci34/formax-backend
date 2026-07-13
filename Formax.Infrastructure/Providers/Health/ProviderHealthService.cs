using System;
using System.Collections.Generic;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Health;

/// <summary>
/// Merkezi sağlayıcı sağlık servisi.
/// Kaydedilen başarı/başarısızlık olaylarından metrikleri (sayaçlar, zaman damgaları, ortalama yanıt
/// süresi, availability) günceller. Bunlar salt bookkeeping'dir.
///
/// Bu faz İSKELET: <see cref="ProviderHealth.Status"/> TÜRETİLMEZ — hangi metriğin hangi eşikle
/// Healthy/Warning/Critical/Offline'a dönüşeceği (health algoritması) YAZILMAMIŞTIR. Durum yalnızca
/// <see cref="SetStatus"/> ile dışarıdan atanır (gelecekteki sınıflandırma katmanı için).
///
/// KAPSAM DIŞI: provider çağırma, HTTP, otomatik sağlık kontrolü, timer, DB — burada YOKTUR.
/// </summary>
public sealed class ProviderHealthService : IProviderHealthService
{
    private readonly ProviderHealthRegistry _registry;

    public ProviderHealthService(ProviderHealthRegistry registry)
    {
        _registry = registry;
    }

    public void RecordSuccess(string providerName, ProviderCapability capability, TimeSpan responseTime)
    {
        var now = DateTimeOffset.UtcNow;

        _registry.AddOrUpdate(
            providerName,
            capability,
            create: () => ProviderHealth.Initial(providerName, capability) with
            {
                LastSuccess = now,
                ConsecutiveFailures = 0,
                TotalRequests = 1,
                SuccessfulRequests = 1,
                LastResponseTime = responseTime,
                AverageResponseTime = responseTime
            },
            update: h => h with
            {
                LastSuccess = now,
                ConsecutiveFailures = 0,
                TotalRequests = h.TotalRequests + 1,
                SuccessfulRequests = h.SuccessfulRequests + 1,
                LastResponseTime = responseTime,
                AverageResponseTime = IncrementalAverage(h.AverageResponseTime, h.TotalRequests, responseTime)
            });
    }

    public void RecordFailure(string providerName, ProviderCapability capability, TimeSpan responseTime, string? error)
    {
        var now = DateTimeOffset.UtcNow;

        _registry.AddOrUpdate(
            providerName,
            capability,
            create: () => ProviderHealth.Initial(providerName, capability) with
            {
                LastFailure = now,
                ConsecutiveFailures = 1,
                TotalRequests = 1,
                FailedRequests = 1,
                LastResponseTime = responseTime,
                AverageResponseTime = responseTime,
                LastError = error
            },
            update: h => h with
            {
                LastFailure = now,
                ConsecutiveFailures = h.ConsecutiveFailures + 1,
                TotalRequests = h.TotalRequests + 1,
                FailedRequests = h.FailedRequests + 1,
                LastResponseTime = responseTime,
                AverageResponseTime = IncrementalAverage(h.AverageResponseTime, h.TotalRequests, responseTime),
                LastError = error
            });
    }

    public ProviderHealth? GetHealth(string providerName, ProviderCapability capability) =>
        _registry.TryGet(providerName, capability, out var health) ? health : null;

    public IReadOnlyList<ProviderHealth> GetAll() => _registry.All;

    public void SetStatus(string providerName, ProviderCapability capability, ProviderHealthStatus status) =>
        _registry.AddOrUpdate(
            providerName,
            capability,
            create: () => ProviderHealth.Initial(providerName, capability) with { Status = status },
            update: h => h with { Status = status });

    /// <summary>
    /// Artımlı ortalama: <paramref name="previousCount"/> örnek üzerinden ortalamayı, yeni örnekle günceller.
    /// (Sağlık algoritması değil; salt metrik toplama.)
    /// </summary>
    private static TimeSpan IncrementalAverage(TimeSpan currentAverage, long previousCount, TimeSpan sample)
    {
        var newCount = previousCount + 1;
        if (newCount <= 0)
            return sample;

        var deltaTicks = (sample.Ticks - currentAverage.Ticks) / newCount;
        return TimeSpan.FromTicks(currentAverage.Ticks + deltaTicks);
    }
}
