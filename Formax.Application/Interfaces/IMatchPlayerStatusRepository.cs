using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IMatchPlayerStatusRepository
    {
        // ── Reads (synchronous) ────────────────────────────────────────────

        List<MatchPlayerStatus> GetByMatchId(int matchId);

        // ── Writes (async) ─────────────────────────────────────────────────

        /// <summary>
        /// Deletes existing statuses for the match then inserts the new set.
        /// </summary>
        Task ReplaceAsync(int matchId, IEnumerable<MatchPlayerStatus> statuses, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
