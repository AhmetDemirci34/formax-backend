using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public class MatchMomentumRepository : IMatchMomentumRepository
    {
        private readonly FormaxDbContext _context;

        public MatchMomentumRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public List<MatchMomentumSnapshot> GetByMatchId(int matchId, int maxCount = 90)
            => _context.MatchMomentumSnapshots
                .Where(x => x.MatchId == matchId)
                .OrderBy(x => x.MinuteBucket)
                .Take(maxCount)
                .ToList();

        public async Task AddAsync(MatchMomentumSnapshot snapshot, CancellationToken ct = default)
        {
            _context.MatchMomentumSnapshots.Add(snapshot);
            await Task.CompletedTask;   // Add is synchronous; caller calls SaveChangesAsync
        }

        public async Task TrimAsync(int matchId, int maxKeep = 120, CancellationToken ct = default)
        {
            var all = await _context.MatchMomentumSnapshots
                .Where(x => x.MatchId == matchId)
                .OrderByDescending(x => x.MinuteBucket)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync(ct);

            if (all.Count <= maxKeep) return;

            var toDelete = all.Skip(maxKeep).ToList();
            _context.MatchMomentumSnapshots.RemoveRange(toDelete);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
