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
        /// </summary>
        Task AddNewEventsAsync(
            int matchId,
            IEnumerable<MatchLiveEvent> events,
            CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
