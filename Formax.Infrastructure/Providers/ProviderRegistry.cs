using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers;

/// <summary>
/// DI'a kayıtlı tüm sağlayıcıları tutan salt-okunur kayıt defteri.
///
/// Platform ilkesi: sağlayıcı sayısı SINIRSIZDIR ve burada hiçbir varsayım yoktur.
/// Keşif iki eksende yapılabilir:
///  • Tip bazlı: <see cref="Enabled{T}"/> (ör. tüm <see cref="IFixtureProvider"/>'lar).
///  • Yetenek bazlı: <see cref="WithCapability"/> — manifestten OTOMATİK okunur.
/// Her ikisi de manifest metadata'sını (etkinlik/öncelik/sınıf) dikkate alır.
/// </summary>
public sealed class ProviderRegistry
{
    private readonly IReadOnlyList<IDataProvider> _all;

    public ProviderRegistry(IEnumerable<IDataProvider> providers)
    {
        _all = (providers ?? Enumerable.Empty<IDataProvider>())
            .Where(p => p is not null)
            .ToList();
    }

    /// <summary>Kayıtlı tüm sağlayıcılar (tür/etkinlik ayrımı olmadan).</summary>
    public IReadOnlyList<IDataProvider> All => _all;

    /// <summary>Toplam kayıtlı sağlayıcı sayısı.</summary>
    public int Count => _all.Count;

    public IReadOnlyList<IFixtureProvider> FixtureProviders => Enabled<IFixtureProvider>();

    public IReadOnlyList<ILiveProvider> LiveProviders => Enabled<ILiveProvider>();

    public IReadOnlyList<INewsProvider> NewsProviders => Enabled<INewsProvider>();

    /// <summary>
    /// Belirli bir sözleşmeyi (<typeparamref name="T"/>) uygulayan, ETKİN sağlayıcıları
    /// önceliğe göre (yüksek → düşük), eşitlikte ada göre kararlı biçimde sıralı döndürür.
    /// </summary>
    public IReadOnlyList<T> Enabled<T>() where T : IDataProvider =>
        _all
            .OfType<T>()
            .Where(p => p.Manifest.Enabled)
            .OrderByDescending(p => p.Manifest.Priority)
            .ThenBy(p => p.Manifest.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Verilen yeteneği manifestinde bildiren ETKİN sağlayıcılar (önceliğe göre sıralı).
    /// Yetenekler manifestten otomatik okunur; tip bazlı keşiften bağımsız kullanılabilir.
    ///
    /// <paramref name="includePaid"/> = false ise ücretli sağlayıcılar hariç tutulur —
    /// böylece çekirdek kural (sistem yalnızca ücretsiz kaynaklarla da çalışabilmeli) uygulanabilir.
    /// </summary>
    public IReadOnlyList<IDataProvider> WithCapability(ProviderCapability capability, bool includePaid = true) =>
        _all
            .Where(p => p.Manifest.Enabled
                        && p.Manifest.Supports(capability)
                        && (includePaid || !p.Manifest.RequiresPayment))
            .OrderByDescending(p => p.Manifest.Priority)
            .ThenBy(p => p.Manifest.Name, StringComparer.Ordinal)
            .ToList();
}
