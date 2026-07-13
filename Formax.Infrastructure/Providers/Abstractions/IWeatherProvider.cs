using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>Hava durumu (weather) verisi sağlayan kaynak adaptörü.</summary>
public interface IWeatherProvider : IDataProvider
{
    Task<ProviderResult> FetchWeatherAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
