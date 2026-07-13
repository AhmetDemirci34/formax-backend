using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>Oyuncu verisi sağlayan kaynak adaptörü.</summary>
public interface IPlayerProvider : IDataProvider
{
    Task<ProviderResult> FetchPlayersAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
