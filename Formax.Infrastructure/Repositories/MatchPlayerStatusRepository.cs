using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class MatchPlayerStatusRepository : IMatchPlayerStatusRepository
    {
        private readonly FormaxDbContext _context;

        public MatchPlayerStatusRepository(FormaxDbContext context)
        {
            _context = context;
        }

        // ── Synchronous reads ──────────────────────────────────────────────────

        public List<MatchPlayerStatus> GetByMatchId(int matchId)
            => _context.MatchPlayerStatuses
                .Where(x => x.MatchId == matchId)
                .ToList();

        // ── Async writes ───────────────────────────────────────────────────────

        public async Task ReplaceAsync(
            int matchId,
            IEnumerable<MatchPlayerStatus> statuses,
            CancellationToken ct = default)
        {
            var existing = await _context.MatchPlayerStatuses
                .Where(x => x.MatchId == matchId)
                .ToListAsync(ct);

            _context.MatchPlayerStatuses.RemoveRange(existing);

            foreach (var s in statuses)
                _context.MatchPlayerStatuses.Add(s);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
