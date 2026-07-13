using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IMatchLiveStatsRepository
    {
        // ── Synchronous reads ──────────────────────────────────────────────────

        MatchLiveStats? GetByMatchId(int matchId);

        /// <summary>
        /// Batch read — loads current stats rows for all supplied matchIds in
        /// one query.  Used by the ingestion job for dirty-checking before
        /// deciding whether to issue per-match detailed stat requests.
        /// </summary>
        List<MatchLiveStats> GetByMatchIds(IEnumerable<int> matchIds);

        // ── Async writes ───────────────────────────────────────────────────────

        Task UpsertAsync(MatchLiveStats stats, CancellationToken ct = default);

        /// <summary>
        /// Upsert overload that reuses an already-loaded, EF-tracked entity to
        /// eliminate the internal SELECT that the parameterless overload performs.
        /// Pass the entity returned by <see cref="GetByMatchIds"/> as
        /// <paramref name="existingTracked"/>; pass null when no row exists yet.
        /// </summary>
        Task UpsertAsync(MatchLiveStats incoming, MatchLiveStats? existingTracked, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
