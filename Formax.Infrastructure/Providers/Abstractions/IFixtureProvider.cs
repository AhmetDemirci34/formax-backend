using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>
/// Fikstür (maç programı / keşif) verisi sağlayan kaynak adaptörü.
/// Somut implementasyonlar ileride eklenecek; bu iskelette yalnızca sözleşme tanımlıdır.
/// </summary>
public interface IFixtureProvider : IDataProvider
{
    Task<ProviderResult> FetchFixturesAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
