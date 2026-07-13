using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Configuration;

/// <summary>
/// Merkezi yapılandırma kayıt defteri: tüm <see cref="ProviderConfiguration"/>'ları tutar.
/// Thread-safe'dir (ileride IConfiguration/yönetim paneli tarafından eşzamanlı güncellenebilmesi için).
/// (provider adı + capability) çifti başına tek yapılandırma tutulur (upsert).
/// </summary>
public sealed class ProviderConfigurationRegistry
{
    private readonly ConcurrentDictionary<string, ProviderConfiguration> _configs =
        new(StringComparer.Ordinal);

    /// <summary>Yapılandırmayı ekler veya (aynı anahtarda) günceller.</summary>
    public void Register(ProviderConfiguration configuration)
    {
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));
        _configs[configuration.Key] = configuration;
    }

    /// <summary>Bir yapılandırmayı kaldırır.</summary>
    public bool Remove(string providerName, ProviderCapability capability) =>
        _configs.TryRemove(ProviderConfiguration.BuildKey(providerName, capability), out _);

    /// <summary>Bir provider+capability için yapılandırmayı getirir.</summary>
    public bool TryGet(string providerName, ProviderCapability capability, out ProviderConfiguration? configuration) =>
        _configs.TryGetValue(ProviderConfiguration.BuildKey(providerName, capability), out configuration);

    /// <summary>Kayıtlı tüm yapılandırmalar.</summary>
    public IReadOnlyList<ProviderConfiguration> All => _configs.Values.ToList();

    /// <summary>Toplam yapılandırma sayısı.</summary>
    public int Count => _configs.Count;

    /// <summary>Yalnızca etkin yapılandırmalar, önceliğe göre sıralı.</summary>
    public IReadOnlyList<ProviderConfiguration> Enabled() =>
        _configs.Values
            .Where(c => c.Enabled)
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.ProviderName, StringComparer.Ordinal)
            .ToList();

    /// <summary>Belirli bir sağlayıcıya ait yapılandırmalar.</summary>
    public IReadOnlyList<ProviderConfiguration> ForProvider(string providerName) =>
        _configs.Values
            .Where(c => string.Equals(c.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Priority)
            .ToList();

    /// <summary>Belirli bir yeteneğe ait yapılandırmalar.</summary>
    public IReadOnlyList<ProviderConfiguration> ForCapability(ProviderCapability capability) =>
        _configs.Values
            .Where(c => c.Capability == capability)
            .OrderByDescending(c => c.Priority)
            .ToList();
}
