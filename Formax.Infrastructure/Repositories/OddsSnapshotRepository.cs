using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// Radar Odds Movement (R.11.2) — EF Core persistence for odds readings and movements.
    /// </summary>
    public sealed class OddsSnapshotRepository : IOddsSnapshotRepository
    {
        private readonly FormaxDbContext _context;

        public OddsSnapshotRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task AddSnapshotAsync(OddsSnapshot snapshot, CancellationToken ct = default)
            => await _context.OddsSnapshots.AddAsync(snapshot, ct);

        public async Task<OddsSnapshot?> GetLatestSnapshotAsync(int matchId, CancellationToken ct = default)
            => await _context.OddsSnapshots
                .AsNoTracking()
                .Where(x => x.MatchId == matchId)
                .OrderByDescending(x => x.CapturedAtUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);

        public async Task AddMovementAsync(OddsMovementSnapshot movement, CancellationToken ct = default)
            => await _context.OddsMovementSnapshots.AddAsync(movement, ct);

        public async Task<IReadOnlyList<OddsMovementSnapshot>> GetMovementsByMatchAsync(
            int matchId, CancellationToken ct = default)
            => await _context.OddsMovementSnapshots
                .AsNoTracking()
                .Where(x => x.MatchId == matchId)
                .OrderBy(x => x.ComputedAtUtc)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
