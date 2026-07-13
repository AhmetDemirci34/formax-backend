using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IMatchLineupRepository
    {
        // ── Reads (synchronous — used from synchronous use cases) ──────────

        MatchLineup? GetByMatchId(int matchId);

        List<MatchLineupPlayer> GetPlayersByMatchId(int matchId);

        // ── Writes (async — used from background jobs) ─────────────────────

        Task UpsertAsync(MatchLineup lineup, CancellationToken ct = default);

        /// <summary>
        /// Deletes existing players for the match then inserts the new set.
        /// </summary>
        Task ReplacePlayersAsync(int matchId, IEnumerable<MatchLineupPlayer> players, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
