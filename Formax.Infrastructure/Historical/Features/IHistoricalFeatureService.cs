using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Historical.Features;

/// <summary>
/// Historical Database'den (salt-okunur) bir maç için Probability Engine özelliklerini üretir.
/// Deterministik + leakage-free (yalnızca maç tarihinden önceki veri).
/// </summary>
public interface IHistoricalFeatureService
{
    /// <summary>Verilen tarihsel maç için özellik vektörünü hesaplar. Maç yoksa null.</summary>
    Task<MatchFeatureVector?> ComputeAsync(int historicalMatchId, CancellationToken cancellationToken = default);
}
