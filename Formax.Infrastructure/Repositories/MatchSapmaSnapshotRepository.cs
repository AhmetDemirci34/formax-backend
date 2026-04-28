using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    public sealed class MatchSapmaSnapshotRepository : IMatchSapmaSnapshotRepository
    {
        private readonly FormaxDbContext _context;

        public MatchSapmaSnapshotRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<MatchSapmaSnapshot>> GetFreshByMatchIdsAsync(IReadOnlyList<int> matchIds, DateTime utcNow)
        {
            if (matchIds == null || matchIds.Count == 0)
                return Array.Empty<MatchSapmaSnapshot>();

            return await _context.MatchSapmaSnapshots
                .AsNoTracking()
                .Where(x => matchIds.Contains(x.MatchId) && x.ExpiresAtUtc > utcNow)
                .ToListAsync();
        }

        public async Task UpsertAsync(MatchSapmaSnapshot snapshot)
        {
            var existing = await _context.MatchSapmaSnapshots
                .FirstOrDefaultAsync(x => x.MatchId == snapshot.MatchId);

            if (existing == null)
            {
                _context.MatchSapmaSnapshots.Add(snapshot);
                return;
            }

            existing.Sapma = snapshot.Sapma;
            existing.Bolge = snapshot.Bolge;
            existing.SessizMi = snapshot.SessizMi;
            existing.ComputedAtUtc = snapshot.ComputedAtUtc;
            existing.ExpiresAtUtc = snapshot.ExpiresAtUtc;
        }

        public Task SaveChangesAsync()
        {
            return _context.SaveChangesAsync();
        }
    }
}