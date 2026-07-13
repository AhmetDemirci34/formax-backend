using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Sources.Collection
{
    /// <summary>
    /// Radar Source Engine (R.8.3) — contract every collector implements.
    ///
    /// Guarantees (enforced by <see cref="SourceCollectorBase"/>):
    ///   - never throws; failure is returned as a failed result
    ///   - does not retry (retry/failover is the engine's decision)
    ///   - returns raw items only (no normalization)
    /// </summary>
    public interface ISourceCollector
    {
        /// <summary>The source type this collector handles (factory resolves on this).</summary>
        SourceType SupportedType { get; }

        Task<SourceCollectorResult> CollectAsync(SourceCollectorContext context, CancellationToken ct = default);
    }
}
