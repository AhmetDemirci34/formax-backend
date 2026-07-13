using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Persistence.H2H;

/// <summary>
/// GDP H2H kalıcılaştırma sözleşmesi (Canonical Domain <see cref="Formax.Domain.Entities.HeadToHead"/>).
/// Idempotenttir: aynı FormaxMatchId için aynı geçmişle tekrar çağrıldığında Unchanged döner.
/// </summary>
public interface IGdpH2HPersister
{
    Task<GdpH2HPersistOutcome> PersistAsync(GdpH2HPersistRequest request, CancellationToken cancellationToken = default);
}
