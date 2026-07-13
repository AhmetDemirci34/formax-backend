using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Odds Movement (R.11.2) — persistence for odds readings and the derived
    /// movement time series.
    /// </summary>
    public interface IOddsSnapshotRepository
    {
        Task AddSnapshotAsync(OddsSnapshot snapshot, CancellationToken ct = default);

        /// <summary>Most recent captured reading for a match, or null if none yet.</summary>
        Task<OddsSnapshot?> GetLatestSnapshotAsync(int matchId, CancellationToken ct = default);

        Task AddMovementAsync(OddsMovementSnapshot movement, CancellationToken ct = default);

        /// <summary>All movements for a match, oldest first (diagnostics/history).</summary>
        Task<IReadOnlyList<OddsMovementSnapshot>> GetMovementsByMatchAsync(
            int matchId, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
