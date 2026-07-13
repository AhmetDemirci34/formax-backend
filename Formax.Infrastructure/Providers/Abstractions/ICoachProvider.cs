using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>Teknik direktör (coach) verisi sağlayan kaynak adaptörü.</summary>
public interface ICoachProvider : IDataProvider
{
    Task<ProviderResult> FetchCoachesAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
