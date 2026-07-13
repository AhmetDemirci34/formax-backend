using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// Radar Source Engine (R.8.5) — EF Core persistence for staged source items.
    /// </summary>
    public sealed class StagingRepository : IStagingRepository
    {
        private readonly FormaxDbContext _context;

        public StagingRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task AddRangeAsync(IEnumerable<StagedSourceItem> items, CancellationToken ct = default)
            => await _context.StagedSourceItems.AddRangeAsync(items, ct);

        public Task<int> CountPendingAsync(CancellationToken ct = default)
            => _context.StagedSourceItems.CountAsync(x => !x.Processed, ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
