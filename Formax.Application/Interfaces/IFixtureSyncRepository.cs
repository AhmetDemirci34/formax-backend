using Formax.Domain.Entities;

namespace Formax.Application.Interfaces;

/// <summary>
/// Persistence contract for FixtureSyncJob.
///
/// Design: batch-oriented to avoid N+1 queries.
/// All lookups accept sets of external IDs and return dictionaries
/// so the job can resolve many records with two DB round-trips total.
/// </summary>
public interface IFixtureSyncRepository
{
    // ── Batch read (2 queries per cycle total) ─────────────────────────────────

    /// <summary>
    /// Returns all teams whose ExternalTeamId is in <paramref name="externalIds"/>.
    /// Key = ExternalTeamId.
    /// </summary>
    Dictionary<string, Team> GetTeamsByExternalIds(IEnumerable<string> externalIds);

    /// <summary>
    /// Returns all matches whose ExternalMatchId is in <paramref name="externalIds"/>.
    /// Key = ExternalMatchId.
    /// </summary>
    Dictionary<string, Match> GetMatchesByExternalIds(IEnumerable<string> externalIds);

    // ── Write (tracked entities — EF handles the rest) ─────────────────────────

    void AddTeam(Team team);

    void AddMatch(Match match);

    Task SaveChangesAsync(CancellationToken ct = default);
}
