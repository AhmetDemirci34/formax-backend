using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Source Engine (R.8.1) — persistence contract for per-source runtime status.
    /// </summary>
    public interface ISourceStatusRepository
    {
        Task<IReadOnlyList<SourceStatus>> GetAllAsync(CancellationToken ct = default);

        Task<SourceStatus?> GetBySourceIdAsync(int sourceId, CancellationToken ct = default);

        /// <summary>Insert or update by <see cref="SourceStatus.SourceId"/>.</summary>
        Task<SourceStatus> UpsertAsync(SourceStatus status, CancellationToken ct = default);

        /// <summary>Ensure a default status row exists for the given source id.
        /// No-op if one is already present. Returns the existing or created row.</summary>
        Task<SourceStatus> EnsureExistsAsync(int sourceId, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
