using System.Collections.Generic;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Configuration;

/// <summary>
/// Merkezi sağlayıcı yapılandırma servisi sözleşmesi.
/// <see cref="Load"/> metodu, ileride IConfiguration/appsettings/yönetim panelinden bağlanan
/// yapılandırmaların topluca beslenmesi için tasarlanmıştır — servis kendisi hiçbir kaynağı OKUMAZ.
/// </summary>
public interface IProviderConfigurationService
{
    /// <summary>Tek bir yapılandırmayı ekler/günceller.</summary>
    void Set(ProviderConfiguration configuration);

    /// <summary>
    /// Bir yapılandırma kümesini topluca yükler (upsert). Dış kaynaktan (IConfiguration/panel)
    /// bağlanan listeyi buraya vermek için tasarlanmış giriş noktasıdır.
    /// </summary>
    void Load(IEnumerable<ProviderConfiguration> configurations);

    /// <summary>Bir provider+capability için yapılandırma; yoksa null.</summary>
    ProviderConfiguration? Get(string providerName, ProviderCapability capability);

    /// <summary>Tüm yapılandırmalar.</summary>
    IReadOnlyList<ProviderConfiguration> GetAll();

    /// <summary>Belirli bir sağlayıcıya ait yapılandırmalar.</summary>
    IReadOnlyList<ProviderConfiguration> GetForProvider(string providerName);

    /// <summary>Bir yapılandırmayı kaldırır.</summary>
    bool Remove(string providerName, ProviderCapability capability);
}
