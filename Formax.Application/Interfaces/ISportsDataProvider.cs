using Formax.Application.DTOs.Fixtures;
using Formax.Application.DTOs.Lineup;
using Formax.Application.DTOs.Live;
using Formax.Application.DTOs.Standings;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Abstraction over an external sports data API.
    /// Implementations are vendor-specific (api-football, etc.).
    /// </summary>
    public interface ISportsDataProvider
    {
        // ── Sprint 0: Fixture sync ────────────────────────────────────────────

        /// <summary>
        /// Fetches all fixtures in the given date window (inclusive).
        /// Used by FixtureSyncJob to auto-create / update matches.
        /// Returns an empty list on provider failure — never throws.
        /// </summary>
        Task<List<SportsFixtureResult>> GetFixturesAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken ct = default);

        /// <summary>
        /// Fetches a team's most recent finished matches (results) by external team id.
        /// Used by HistoricalSyncJob to backfill past matches so BuildTeamComparison
        /// has data. Returns an empty list on provider failure — never throws.
        /// Reuses the SportsFixtureResult shape (a past result is a scored fixture).
        /// </summary>
        Task<List<SportsFixtureResult>> GetTeamRecentResultsAsync(
            string externalTeamId,
            CancellationToken ct = default);

        // ── Sprint 1: Lineup ──────────────────────────────────────────────────

        /// <summary>
        /// Fetches the official starting XI and bench for a fixture.
        /// Returns null when the provider has no lineup data yet (lineup not released).
        /// </summary>
        Task<SportsLineupResult?> GetOfficialLineupAsync(
            string matchExternalId,
            CancellationToken ct = default);

        /// <summary>
        /// Fetches injury / suspension / doubtful statuses for a fixture.
        /// Returns an empty list when no data is available.
        /// </summary>
        Task<List<SportsPlayerStatusResult>> GetPlayerStatusesAsync(
            string matchExternalId,
            CancellationToken ct = default);

        // ── Sprint 2: Standings & Competition Context ─────────────────────────

        /// <summary>
        /// Fetches the current league standings table for a given external league + season.
        /// Returns an empty list when the provider has no data.
        /// </summary>
        Task<List<SportsStandingEntry>> GetLeagueStandingsAsync(
            string leagueExternalId,
            int season,
            CancellationToken ct = default);

        /// <summary>
        /// Fetches competition context (type, stage, round) for a specific fixture.
        /// Returns null when the provider has no data.
        /// </summary>
        Task<SportsCompetitionContext?> GetCompetitionContextAsync(
            string matchExternalId,
            CancellationToken ct = default);

        // ── Sprint 3: Live match intelligence ─────────────────────────────────

        /// <summary>
        /// Single-call batch endpoint: returns a lightweight summary for EVERY
        /// currently-live fixture (score + clock only).  Use this once per cycle
        /// to determine which matches have changed before issuing per-match
        /// detailed stat requests.
        /// </summary>
        Task<List<SportsLiveBatchEntry>> GetAllLiveFixturesAsync(
            CancellationToken ct = default);

        /// <summary>
        /// Fetches the current live statistics snapshot for a fixture.
        /// When <paramref name="batchEntry"/> is supplied (score + clock already fetched
        /// by the batch endpoint), the redundant GET /fixtures?id= call is skipped and
        /// only GET /fixtures/statistics?fixture= is issued (1 request instead of 2).
        /// Pass null only from callers that do not have a batch entry (e.g. GetLiveMomentumAsync).
        /// </summary>
        Task<SportsLiveStats?> GetLiveMatchStatsAsync(
            string matchExternalId,
            SportsLiveBatchEntry? batchEntry,
            CancellationToken ct = default);

        /// <summary>
        /// Fetches the event timeline (goals, cards, subs, VAR) for a fixture.
        /// Returns an empty list when no events are available.
        /// </summary>
        Task<List<SportsLiveEvent>> GetLiveMatchEventsAsync(
            string matchExternalId,
            CancellationToken ct = default);

        /// <summary>
        /// Derives a momentum snapshot for a fixture from available live stats.
        /// Returns null when no stats are available to derive from.
        /// </summary>
        Task<SportsLiveMomentum?> GetLiveMomentumAsync(
            string matchExternalId,
            CancellationToken ct = default);
    }
}
