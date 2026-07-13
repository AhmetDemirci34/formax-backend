using System.Collections.Generic;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>
/// Bir sağlayıcının kendini tanımlayan meta verisi.
/// Her sağlayıcı tek bir manifest üzerinden adını, sınıfını, önceliğini, etkinliğini ve
/// sunduğu yetenekleri bildirir. Salt-okunur ve yan etkisizdir (DB'ye yazmaz).
///
/// NOT (FAZ 10): Yenileme sıklığı (RefreshInterval) artık manifestte DEĞİLDİR — tüm zamanlama
/// merkezi olarak <c>ProviderScheduler</c> / <c>ProviderSchedule</c> üzerinden yönetilir.
/// </summary>
public sealed record ProviderManifest
{
    /// <summary>Benzersiz sağlayıcı adı (ör. "openligadb", "thesportsdb").</summary>
    public required string Name { get; init; }

    /// <summary>Erişim/maliyet sınıfı. Varsayılan: ücretsiz.</summary>
    public ProviderCategory Category { get; init; } = ProviderCategory.Free;

    /// <summary>Yüksek değer önce denenir.</summary>
    public int Priority { get; init; }

    /// <summary>Çalışma zamanında devre dışı bırakılabilir.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Bu sağlayıcının sunabildiği veri türleri.</summary>
    public IReadOnlySet<ProviderCapability> Capabilities { get; init; } = EmptyCapabilities;

    private static readonly IReadOnlySet<ProviderCapability> EmptyCapabilities =
        new HashSet<ProviderCapability>();

    /// <summary>Sağlayıcı verilen yeteneği sunuyor mu?</summary>
    public bool Supports(ProviderCapability capability) => Capabilities.Contains(capability);

    /// <summary>Ücretli mi? (çekirdek kural: asla zorunlu olmamalı)</summary>
    public bool RequiresPayment => Category == ProviderCategory.Paid;

    /// <summary>Tamamen ücretsiz mi?</summary>
    public bool IsFree => Category == ProviderCategory.Free;

    /// <summary>Manifest oluşturmak için ergonomik fabrika.</summary>
    public static ProviderManifest Create(
        string name,
        IEnumerable<ProviderCapability> capabilities,
        ProviderCategory category = ProviderCategory.Free,
        int priority = 0,
        bool enabled = true) => new()
        {
            Name = name,
            Category = category,
            Priority = priority,
            Enabled = enabled,
            Capabilities = capabilities is null
                ? EmptyCapabilities
                : new HashSet<ProviderCapability>(capabilities)
        };
}
