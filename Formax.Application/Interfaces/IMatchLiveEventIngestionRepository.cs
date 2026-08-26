using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Write-side repository for <see cref="MatchLiveEvent"/> used exclusively by the
    /// live ingestion job. Separate from the read-side live event providers to keep
    /// concerns clean.
    /// </summary>
    public interface IMatchLiveEventIngestionRepository
    {
        // ── Synchronous reads ──────────────────────────────────────────────────

        List<MatchLiveEvent> GetByMatchId(int matchId);

        // ── Async writes ───────────────────────────────────────────────────────

        /// <summary>
        /// Inserts only events that do not already exist for this match.
        /// Deduplication key: (MatchId, Minute, EventType, Team).
        /// Returns the events that were actually newly inserted (empty if all duplicates),
        /// so callers can react to genuinely new events (e.g. notification fan-out) exactly once.
        /// </summary>
        Task<List<MatchLiveEvent>> AddNewEventsAsync(
            int matchId,
            IEnumerable<MatchLiveEvent> events,
            CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
