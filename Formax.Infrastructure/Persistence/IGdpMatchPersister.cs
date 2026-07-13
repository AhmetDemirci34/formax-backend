using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Persistence;

/// <summary>
/// GDP maç kalıcılaştırma sözleşmesi. Idempotenttir: aynı veriyle tekrar çağrıldığında Unchanged döner.
/// Transaction kullanır; hata durumunda rollback yapar.
/// </summary>
public interface IGdpMatchPersister
{
    Task<GdpPersistOutcome> PersistAsync(GdpPersistRequest request, CancellationToken cancellationToken = default);
}
