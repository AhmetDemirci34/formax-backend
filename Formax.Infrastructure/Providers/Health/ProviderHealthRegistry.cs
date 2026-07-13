using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Health;

/// <summary>
/// Merkezi sağlık kayıt defteri: her sağlayıcı+yetenek çifti için <see cref="ProviderHealth"/> tutar.
/// Thread-safe'dir (ileride farklı çağrı yollarından eşzamanlı güncellenebilmesi için).
/// </summary>
public sealed class ProviderHealthRegistry
{
    private readonly ConcurrentDictionary<string, ProviderHealth> _health =
        new(StringComparer.Ordinal);

    /// <summary>Atomik ekle-veya-güncelle: kayıt yoksa <paramref name="create"/>, varsa <paramref name="update"/>.</summary>
    public ProviderHealth AddOrUpdate(
        string providerName,
        ProviderCapability capability,
        Func<ProviderHealth> create,
        Func<ProviderHealth, ProviderHealth> update)
    {
        if (create is null) throw new ArgumentNullException(nameof(create));
        if (update is null) throw new ArgumentNullException(nameof(update));

        var key = ProviderHealth.BuildKey(providerName, capability);
        return _health.AddOrUpdate(key, _ => create(), (_, existing) => update(existing));
    }

    /// <summary>Bir sağlayıcı+yetenek için sağlık anlık görüntüsünü getirir.</summary>
    public bool TryGet(string providerName, ProviderCapability capability, out ProviderHealth? health) =>
        _health.TryGetValue(ProviderHealth.BuildKey(providerName, capability), out health);

    /// <summary>Kayıtlı tüm sağlık kayıtları.</summary>
    public IReadOnlyList<ProviderHealth> All => _health.Values.ToList();

    /// <summary>Toplam kayıt sayısı.</summary>
    public int Count => _health.Count;

    /// <summary>Belirli bir duruma sahip kayıtlar.</summary>
    public IReadOnlyList<ProviderHealth> ForStatus(ProviderHealthStatus status) =>
        _health.Values.Where(h => h.Status == status).ToList();
}
