using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Source Engine (R.8.1) — read-facing registry API consumed by later
    /// Source Engine layers (scheduler, collector, failover). Combines persisted
    /// definitions with their runtime status.
    ///
    /// This is the single entry point the rest of the engine uses to ask
    /// "which sources exist, and in what order should I try a failover group?".
    /// It performs no collection itself.
    /// </summary>
    public interface ISourceRegistry
    {
        /// <summary>All definitions (regardless of Enabled/Active).</summary>
        Task<IReadOnlyList<SourceDefinition>> GetDefinitionsAsync(CancellationToken ct = default);

        /// <summary>Definitions that are operator-Enabled AND runtime-Active.</summary>
        Task<IReadOnlyList<SourceDefinition>> GetUsableAsync(CancellationToken ct = default);

        /// <summary>Usable members of a failover group, ordered by ascending Priority.
        /// The first element is the preferred source to try.</summary>
        Task<IReadOnlyList<SourceDefinition>> GetFailoverGroupAsync(
            string failoverGroup, CancellationToken ct = default);

        /// <summary>Status row for a source (null if not yet initialised).</summary>
        Task<SourceStatus?> GetStatusAsync(int sourceId, CancellationToken ct = default);
    }
}
