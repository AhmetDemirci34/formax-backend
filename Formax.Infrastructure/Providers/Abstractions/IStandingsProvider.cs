using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>Puan durumu (standings) verisi sağlayan kaynak adaptörü.</summary>
public interface IStandingsProvider : IDataProvider
{
    Task<ProviderResult> FetchStandingsAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
