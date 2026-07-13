using System;
using System.Collections.Generic;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Health;

/// <summary>
/// Merkezi sağlayıcı sağlık servisi sözleşmesi.
/// <see cref="RecordSuccess"/>/<see cref="RecordFailure"/> dışarıdan (ör. ileride
/// <c>ProviderOrchestrator</c>) çağrılabilecek şekilde tasarlanmıştır — servis kendisi
/// hiçbir provider çağırmaz, HTTP yapmaz, otomatik kontrol tetiklemez.
/// </summary>
public interface IProviderHealthService
{
    /// <summary>Bir başarı olayını kaydeder (metrikleri günceller).</summary>
    void RecordSuccess(string providerName, ProviderCapability capability, TimeSpan responseTime);

    /// <summary>Bir başarısızlık olayını kaydeder (metrikleri + son hatayı günceller).</summary>
    void RecordFailure(string providerName, ProviderCapability capability, TimeSpan responseTime, string? error);

    /// <summary>Bir sağlayıcı+yetenek için sağlık anlık görüntüsü; yoksa null.</summary>
    ProviderHealth? GetHealth(string providerName, ProviderCapability capability);

    /// <summary>Tüm sağlık kayıtları.</summary>
    IReadOnlyList<ProviderHealth> GetAll();

    /// <summary>
    /// Durumu dışarıdan atar. Health algoritması bu fazda YOK; durum sınıflandırmasını
    /// yapacak gelecekteki katman bu metodu kullanacaktır.
    /// </summary>
    void SetStatus(string providerName, ProviderCapability capability, ProviderHealthStatus status);
}
