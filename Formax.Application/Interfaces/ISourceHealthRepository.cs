using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Source Engine (R.8.6) — persistence contract for source health snapshots.
    /// </summary>
    public interface ISourceHealthRepository
    {
        Task<SourceHealthSnapshot?> GetBySourceIdAsync(int sourceId, CancellationToken ct = default);

        Task<IReadOnlyList<SourceHealthSnapshot>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Insert or update by SourceId.</summary>
        Task UpsertAsync(SourceHealthSnapshot snapshot, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
