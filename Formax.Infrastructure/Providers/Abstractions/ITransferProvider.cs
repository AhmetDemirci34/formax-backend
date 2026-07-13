using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>Transfer verisi sağlayan kaynak adaptörü.</summary>
public interface ITransferProvider : IDataProvider
{
    Task<ProviderResult> FetchTransfersAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
