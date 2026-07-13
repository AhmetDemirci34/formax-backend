using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class LeagueStandingRepository : ILeagueStandingRepository
    {
        private readonly FormaxDbContext _context;

        public LeagueStandingRepository(FormaxDbContext context)
        {
            _context = context;
        }

        // ── Synchronous reads ──────────────────────────────────────────────────

        public List<LeagueStanding> GetByLeague(int leagueId, int seasonYear)
            => _context.LeagueStandings
                .Where(x => x.LeagueId == leagueId && x.SeasonYear == seasonYear)
                .OrderBy(x => x.Position)
                .ToList();

        public LeagueStanding? GetByTeam(int leagueId, int seasonYear, int teamId)
            => _context.LeagueStandings
                .FirstOrDefault(x =>
                    x.LeagueId == leagueId &&
                    x.SeasonYear == seasonYear &&
                    x.TeamId == teamId);

        // ── Async writes ───────────────────────────────────────────────────────

        public async Task ReplaceAsync(
            int leagueId,
            int seasonYear,
            IEnumerable<LeagueStanding> standings,
            CancellationToken ct = default)
        {
            var existing = await _context.LeagueStandings
                .Where(x => x.LeagueId == leagueId && x.SeasonYear == seasonYear)
                .ToListAsync(ct);

            _context.LeagueStandings.RemoveRange(existing);

            foreach (var s in standings)
                _context.LeagueStandings.Add(s);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
