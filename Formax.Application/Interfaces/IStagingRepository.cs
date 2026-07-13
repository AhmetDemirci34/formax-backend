using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Source Engine (R.8.5) — persistence contract for the staging table.
    /// </summary>
    public interface IStagingRepository
    {
        Task AddRangeAsync(IEnumerable<StagedSourceItem> items, CancellationToken ct = default);

        /// <summary>Count of items not yet consumed by Intelligence (diagnostics).</summary>
        Task<int> CountPendingAsync(CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
