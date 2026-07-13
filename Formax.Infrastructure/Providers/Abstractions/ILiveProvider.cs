using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>
/// Canlı maç verisi (skor / olay / durum) sağlayan kaynak adaptörü.
/// Somut implementasyonlar ileride eklenecek; bu iskelette yalnızca sözleşme tanımlıdır.
/// </summary>
public interface ILiveProvider : IDataProvider
{
    Task<ProviderResult> FetchLiveAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
