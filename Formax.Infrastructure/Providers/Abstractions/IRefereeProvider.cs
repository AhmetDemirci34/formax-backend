using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>Hakem (referee) verisi sağlayan kaynak adaptörü.</summary>
public interface IRefereeProvider : IDataProvider
{
    Task<ProviderResult> FetchRefereesAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
