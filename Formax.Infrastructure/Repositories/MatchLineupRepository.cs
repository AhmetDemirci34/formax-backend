using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class MatchLineupRepository : IMatchLineupRepository
    {
        private readonly FormaxDbContext _context;

        public MatchLineupRepository(FormaxDbContext context)
        {
            _context = context;
        }

        // ── Synchronous reads ──────────────────────────────────────────────────

        public MatchLineup? GetByMatchId(int matchId)
            => _context.MatchLineups.FirstOrDefault(x => x.MatchId == matchId);

        public List<MatchLineupPlayer> GetPlayersByMatchId(int matchId)
            => _context.MatchLineupPlayers
                .Where(x => x.MatchId == matchId)
                .ToList();

        // ── Async writes ───────────────────────────────────────────────────────

        public async Task UpsertAsync(MatchLineup lineup, CancellationToken ct = default)
        {
            var existing = await _context.MatchLineups
                .FirstOrDefaultAsync(x => x.MatchId == lineup.MatchId, ct);

            if (existing == null)
            {
                _context.MatchLineups.Add(lineup);
            }
            else
            {
                existing.HomeLineupsReleased = lineup.HomeLineupsReleased;
                existing.AwayLineupsReleased = lineup.AwayLineupsReleased;
                existing.ReleasedAt ??= lineup.ReleasedAt;   // only set first time
                existing.FetchedAt = lineup.FetchedAt;
                _context.MatchLineups.Update(existing);
            }
        }

        public async Task ReplacePlayersAsync(
            int matchId,
            IEnumerable<MatchLineupPlayer> players,
            CancellationToken ct = default)
        {
            var existing = await _context.MatchLineupPlayers
                .Where(x => x.MatchId == matchId)
                .ToListAsync(ct);

            _context.MatchLineupPlayers.RemoveRange(existing);

            foreach (var p in players)
                _context.MatchLineupPlayers.Add(p);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
