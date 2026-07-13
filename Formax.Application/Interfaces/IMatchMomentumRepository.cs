using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IMatchMomentumRepository
    {
        // ── Synchronous reads ──────────────────────────────────────────────────

        /// <summary>Returns momentum snapshots ordered ascending by MinuteBucket, capped at maxCount.</summary>
        List<MatchMomentumSnapshot> GetByMatchId(int matchId, int maxCount = 90);

        // ── Async writes ───────────────────────────────────────────────────────

        Task AddAsync(MatchMomentumSnapshot snapshot, CancellationToken ct = default);

        /// <summary>
        /// Trims snapshots to keep only the latest maxKeep rows per match.
        /// Call periodically to prevent unbounded growth.
        /// </summary>
        Task TrimAsync(int matchId, int maxKeep = 120, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
