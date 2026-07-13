using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>İkili karşılaşma geçmişi (head-to-head) verisi sağlayan kaynak adaptörü.</summary>
public interface IH2HProvider : IDataProvider
{
    Task<ProviderResult> FetchH2HAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
