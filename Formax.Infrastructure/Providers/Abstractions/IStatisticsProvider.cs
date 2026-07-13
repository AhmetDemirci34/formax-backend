using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>İstatistik verisi sağlayan kaynak adaptörü.</summary>
public interface IStatisticsProvider : IDataProvider
{
    Task<ProviderResult> FetchStatisticsAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
