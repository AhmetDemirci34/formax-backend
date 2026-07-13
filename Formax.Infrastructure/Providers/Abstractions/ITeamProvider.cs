using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>Takım verisi sağlayan kaynak adaptörü.</summary>
public interface ITeamProvider : IDataProvider
{
    Task<ProviderResult> FetchTeamsAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
