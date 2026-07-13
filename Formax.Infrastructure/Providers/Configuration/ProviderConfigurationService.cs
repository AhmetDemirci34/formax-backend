using System.Collections.Generic;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Configuration;

/// <summary>
/// Merkezi sağlayıcı yapılandırma servisi.
/// Yapılandırmaları <see cref="ProviderConfigurationRegistry"/> üzerinden yönetir.
///
/// KAPSAM DIŞI: gerçek config yükleme, appsettings/IConfiguration okuma, DB — burada YOKTUR.
/// <see cref="Load"/> yalnızca dışarıdan verilen hazır yapılandırmaları belleğe alır; hiçbir kaynağı okumaz.
/// </summary>
public sealed class ProviderConfigurationService : IProviderConfigurationService
{
    private readonly ProviderConfigurationRegistry _registry;

    public ProviderConfigurationService(ProviderConfigurationRegistry registry)
    {
        _registry = registry;
    }

    public void Set(ProviderConfiguration configuration) => _registry.Register(configuration);

    public void Load(IEnumerable<ProviderConfiguration> configurations)
    {
        if (configurations is null)
            return;

        foreach (var configuration in configurations)
        {
            if (configuration is not null)
                _registry.Register(configuration);
        }
    }

    public ProviderConfiguration? Get(string providerName, ProviderCapability capability) =>
        _registry.TryGet(providerName, capability, out var configuration) ? configuration : null;

    public IReadOnlyList<ProviderConfiguration> GetAll() => _registry.All;

    public IReadOnlyList<ProviderConfiguration> GetForProvider(string providerName) =>
        _registry.ForProvider(providerName);

    public bool Remove(string providerName, ProviderCapability capability) =>
        _registry.Remove(providerName, capability);
}
