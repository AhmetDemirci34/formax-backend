using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>Kadro (lineup) verisi sağlayan kaynak adaptörü.</summary>
public interface ILineupProvider : IDataProvider
{
    Task<ProviderResult> FetchLineupsAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
