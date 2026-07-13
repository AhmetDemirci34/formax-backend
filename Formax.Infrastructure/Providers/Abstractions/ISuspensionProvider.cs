using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>Ceza/men (suspension) verisi sağlayan kaynak adaptörü.</summary>
public interface ISuspensionProvider : IDataProvider
{
    Task<ProviderResult> FetchSuspensionsAsync(ProviderRequest request, CancellationToken cancellationToken = default);
}
