using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>
/// Haber / metin verisi sağlayan kaynak adaptörü.
/// Somut implementasyonlar ileride eklenecek; bu iskelette yalnızca sözleşme tanımlıdır.
/// </summary>
public interface INewsProvider : IDataProvider
{
    Task<ProviderResult> FetchNewsAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
