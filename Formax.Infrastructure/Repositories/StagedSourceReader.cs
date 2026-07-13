using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// Radar (R.9.4) — EF Core reader over StagedSourceItems for the Intelligence layer.
    /// Read-only; does not modify staging rows (no Processed flip this sprint).
    /// </summary>
    public sealed class StagedSourceReader : IStagedSourceReader
    {
        private readonly FormaxDbContext _context;

        public StagedSourceReader(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<StagedSourceItem>> GetPendingByTeamAsync(
            int teamId, int take, CancellationToken ct = default)
            => await _context.StagedSourceItems
                .AsNoTracking()
                .Where(x => !x.Processed && x.ResolvedTeamId == teamId)
                .OrderByDescending(x => x.NormalizedAtUtc)
                .Take(take)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<StagedSourceItem>> GetPendingByCategoryAsync(
            SourceCategory category, int take, CancellationToken ct = default)
            => await _context.StagedSourceItems
                .AsNoTracking()
                .Where(x => !x.Processed && x.Category == category)
                .OrderByDescending(x => x.NormalizedAtUtc)
                .Take(take)
                .ToListAsync(ct);
    }
}
