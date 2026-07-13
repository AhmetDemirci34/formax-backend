using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Source Engine (R.8.7) — persistence contract for monitor snapshots.
    /// </summary>
    public interface ISourceMonitorRepository
    {
        Task<SourceMonitorSnapshot?> GetBySourceIdAsync(int sourceId, CancellationToken ct = default);

        Task<IReadOnlyList<SourceMonitorSnapshot>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Insert or update by SourceId.</summary>
        Task UpsertAsync(SourceMonitorSnapshot snapshot, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
