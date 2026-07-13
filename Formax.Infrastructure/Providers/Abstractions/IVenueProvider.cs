using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>Stat/mekan (venue) verisi sağlayan kaynak adaptörü.</summary>
public interface IVenueProvider : IDataProvider
{
    Task<ProviderResult> FetchVenuesAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
