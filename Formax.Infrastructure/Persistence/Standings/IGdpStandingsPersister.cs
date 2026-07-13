using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Persistence.Standings;

/// <summary>
/// GDP puan durumu kalıcılaştırma sözleşmesi (Canonical Domain <see cref="Formax.Domain.Entities.CompetitionStanding"/>).
/// Idempotenttir: aynı competition için aynı geçmişle tekrar çağrıldığında satırlar değişmeden yeniden yazılır.
/// </summary>
public interface IGdpStandingsPersister
{
    Task<GdpStandingsPersistOutcome> PersistAsync(GdpStandingsPersistRequest request, CancellationToken cancellationToken = default);
}
