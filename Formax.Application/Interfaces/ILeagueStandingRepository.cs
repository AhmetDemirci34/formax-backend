using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface ILeagueStandingRepository
    {
        // ── Synchronous reads ──────────────────────────────────────────────────

        List<LeagueStanding> GetByLeague(int leagueId, int seasonYear);

        LeagueStanding? GetByTeam(int leagueId, int seasonYear, int teamId);

        // ── Async writes ───────────────────────────────────────────────────────

        /// <summary>
        /// Deletes existing rows for (leagueId, seasonYear) then inserts the new set.
        /// </summary>
        Task ReplaceAsync(int leagueId, int seasonYear, IEnumerable<LeagueStanding> standings, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
