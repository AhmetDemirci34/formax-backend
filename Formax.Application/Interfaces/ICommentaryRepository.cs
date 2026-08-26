using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Commentary (R.12.1) — persistence for match commentary snapshots.
    /// </summary>
    public interface ICommentaryRepository
    {
        Task<MatchCommentarySnapshot?> GetByMatchIdAsync(int matchId, CancellationToken ct = default);

        /// <summary>
        /// Batch read (perf): commentary snapshots for the given match ids in a SINGLE query.
        /// İçerik aynı; yalnız feed kurulumundaki N sorgu 1'e iner.
        /// </summary>
        Task<IReadOnlyDictionary<int, MatchCommentarySnapshot>> GetByMatchIdsAsync(
            IReadOnlyCollection<int> matchIds, CancellationToken ct = default);

        Task UpsertAsync(MatchCommentarySnapshot snapshot, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
