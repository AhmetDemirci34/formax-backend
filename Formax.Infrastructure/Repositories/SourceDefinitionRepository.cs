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
    /// Radar Source Engine (R.8.1) — EF Core persistence for source definitions.
    /// </summary>
    public sealed class SourceDefinitionRepository : ISourceDefinitionRepository
    {
        private readonly FormaxDbContext _context;

        public SourceDefinitionRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<SourceDefinition>> GetAllAsync(CancellationToken ct = default)
            => await _context.SourceDefinitions
                .AsNoTracking()
                .OrderBy(x => x.FailoverGroup)
                .ThenBy(x => x.Priority)
                .ToListAsync(ct);

        public async Task<SourceDefinition?> GetByKeyAsync(string sourceKey, CancellationToken ct = default)
            => await _context.SourceDefinitions
                .FirstOrDefaultAsync(x => x.SourceKey == sourceKey, ct);

        public async Task<SourceDefinition> UpsertAsync(SourceDefinition definition, CancellationToken ct = default)
        {
            var existing = await _context.SourceDefinitions
                .FirstOrDefaultAsync(x => x.SourceKey == definition.SourceKey, ct);

            if (existing is null)
            {
                _context.SourceDefinitions.Add(definition);
                return definition;
            }

            existing.Name = definition.Name;
            existing.Type = definition.Type;
            existing.Category = definition.Category;
            existing.FailoverGroup = definition.FailoverGroup;
            existing.Priority = definition.Priority;
            existing.Endpoint = definition.Endpoint;
            existing.ScheduleExpr = definition.ScheduleExpr;
            existing.Enabled = definition.Enabled;
            existing.Lifecycle = definition.Lifecycle;
            existing.UpdatedAtUtc = definition.UpdatedAtUtc;
            return existing;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
