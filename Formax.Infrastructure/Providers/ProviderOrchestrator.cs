using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers;

/// <summary>
/// FORMAX GDP giriş noktası.
/// <see cref="ProviderRegistry"/>'den ilgili türdeki TÜM sağlayıcıları otomatik bulur ve çalıştırır.
///
/// Platform davranışı:
///  • Sağlayıcılar birbirinden tamamen bağımsızdır → PARALEL çalıştırılır.
///  • Her sağlayıcı İZOLEdir → biri hata verirse/patlarsa diğerleri etkilenmez
///    (hata, o sağlayıcıya ait <see cref="ProviderResult.Fail"/> sonucuna dönüşür).
///  • Sağlayıcı sayısı sınırsızdır; hiçbir üst limit/varsayım yoktur.
///
/// KAPSAM DIŞI: Normalize / Merge / Duplicate / Conflict katmanları burada YOKTUR.
/// Bu katman yalnızca ham sonuçları toplar ve olduğu gibi döndürür.
/// </summary>
public sealed class ProviderOrchestrator
{
    private readonly ProviderRegistry _registry;

    public ProviderOrchestrator(ProviderRegistry registry)
    {
        _registry = registry;
    }

    public Task<IReadOnlyList<ProviderResult>> CollectFixturesAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => RunAllAsync(_registry.FixtureProviders, p => p.FetchFixturesAsync(request, cancellationToken));

    public Task<IReadOnlyList<ProviderResult>> CollectLiveAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => RunAllAsync(_registry.LiveProviders, p => p.FetchLiveAsync(request, cancellationToken));

    public Task<IReadOnlyList<ProviderResult>> CollectNewsAsync(ProviderRequest request, CancellationToken cancellationToken = default)
        => RunAllAsync(_registry.NewsProviders, p => p.FetchNewsAsync(request, cancellationToken));

    /// <summary>
    /// Herhangi bir sağlayıcı sözleşmesi (<typeparamref name="TProvider"/>) için genel toplama.
    /// Registry'nin generic keşfini (<see cref="ProviderRegistry.Enabled{T}"/>) kullanır; bu sayede
    /// yeni capability interface'leri (Standings, Team, Player, Coach, Venue, Referee, Lineup,
    /// Statistics, H2H, Injury, Suspension, Transfer, Weather, …) Orchestrator DEĞİŞTİRİLMEDEN
    /// otomatik desteklenir. Fetch metodu türe özgü olduğundan çağıran delegate olarak verir:
    /// <c>CollectAsync&lt;IStandingsProvider&gt;((p, ct) =&gt; p.FetchStandingsAsync(request, ct), ct)</c>.
    /// </summary>
    public Task<IReadOnlyList<ProviderResult>> CollectAsync<TProvider>(
        Func<TProvider, CancellationToken, Task<ProviderResult>> fetch,
        CancellationToken cancellationToken = default)
        where TProvider : IDataProvider
        => RunAllAsync(_registry.Enabled<TProvider>(), p => fetch(p, cancellationToken));

    /// <summary>
    /// Verilen sağlayıcı kümesini paralel çalıştırır. Sonuç sırası girdi sırasıyla
    /// (Registry önceliği) aynıdır; tamamlanma sırasından bağımsızdır.
    /// </summary>
    private static async Task<IReadOnlyList<ProviderResult>> RunAllAsync<TProvider>(
        IReadOnlyList<TProvider> providers,
        Func<TProvider, Task<ProviderResult>> fetch)
        where TProvider : IDataProvider
    {
        if (providers.Count == 0)
            return Array.Empty<ProviderResult>();

        var tasks = new Task<ProviderResult>[providers.Count];
        for (var i = 0; i < providers.Count; i++)
            tasks[i] = SafeFetchAsync(providers[i], fetch);

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Tek bir sağlayıcı çağrısını izole eder: istisna veya null sonuç, batch'i bozmaz;
    /// başarısız bir <see cref="ProviderResult"/>'a dönüşür.
    /// </summary>
    private static async Task<ProviderResult> SafeFetchAsync<TProvider>(
        TProvider provider,
        Func<TProvider, Task<ProviderResult>> fetch)
        where TProvider : IDataProvider
    {
        try
        {
            var result = await fetch(provider).ConfigureAwait(false);
            return result ?? ProviderResult.Fail(provider.Manifest.Name, "Sağlayıcı null sonuç döndürdü.");
        }
        catch (Exception ex)
        {
            return ProviderResult.Fail(provider.Manifest.Name, ex.Message);
        }
    }
}
