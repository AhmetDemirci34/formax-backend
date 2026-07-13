using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>Sakatlık (injury) verisi sağlayan kaynak adaptörü.</summary>
public interface IInjuryProvider : IDataProvider
{
    Task<ProviderResult> FetchInjuriesAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
