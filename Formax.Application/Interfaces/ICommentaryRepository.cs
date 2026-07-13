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

        Task UpsertAsync(MatchCommentarySnapshot snapshot, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
