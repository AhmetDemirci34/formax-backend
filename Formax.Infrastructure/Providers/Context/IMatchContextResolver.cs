using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Providers.Abstractions;

namespace Formax.Infrastructure.Providers.Context;

/// <summary>
/// Gerçek Domain Match'ten sağlayıcı isteğini (<see cref="ProviderRequest"/>) besler:
/// takım adları, turnuva ve (ev sahibi konumundan geocode edilen) koordinat.
/// Sağlayıcılar artık sabit koordinat/takım/venue kullanmaz — her şey gerçek maçtan gelir.
/// </summary>
public interface IMatchContextResolver
{
    /// <summary>
    /// Verilen Domain Match Id'sini gerçek maç bağlamına çözer. Match bulunamazsa boş istek döner
    /// (sahte veri üretilmez). Koordinat çözülemezse ilgili alanlar null bırakılır (graceful).
    /// </summary>
    Task<ProviderRequest> ResolveAsync(int matchId, CancellationToken cancellationToken = default);
}
