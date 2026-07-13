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
    /// Radar Source Engine (R.8.6) — EF Core persistence for source health snapshots.
    /// </summary>
    public sealed class SourceHealthRepository : ISourceHealthRepository
    {
        private readonly FormaxDbContext _context;

        public SourceHealthRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<SourceHealthSnapshot?> GetBySourceIdAsync(int sourceId, CancellationToken ct = default)
            => await _context.SourceHealthSnapshots
                .FirstOrDefaultAsync(x => x.SourceId == sourceId, ct);

        public async Task<IReadOnlyList<SourceHealthSnapshot>> GetAllAsync(CancellationToken ct = default)
            => await _context.SourceHealthSnapshots.AsNoTracking().ToListAsync(ct);

        public async Task UpsertAsync(SourceHealthSnapshot snapshot, CancellationToken ct = default)
        {
            var existing = await _context.SourceHealthSnapshots
                .FirstOrDefaultAsync(x => x.SourceId == snapshot.SourceId, ct);

            if (existing is null)
            {
                _context.SourceHealthSnapshots.Add(snapshot);
                return;
            }

            existing.SourceKey = snapshot.SourceKey;
            existing.TotalExecutions = snapshot.TotalExecutions;
            existing.SuccessCount = snapshot.SuccessCount;
            existing.FailureCount = snapshot.FailureCount;
            existing.ConsecutiveFailures = snapshot.ConsecutiveFailures;
            existing.LastSuccessAtUtc = snapshot.LastSuccessAtUtc;
            existing.LastFailureAtUtc = snapshot.LastFailureAtUtc;
            existing.SuccessRate = snapshot.SuccessRate;
            existing.FailureRate = snapshot.FailureRate;
            existing.AvgExecutionTimeMs = snapshot.AvgExecutionTimeMs;
            existing.HealthScore = snapshot.HealthScore;
            existing.Status = snapshot.Status;
            existing.UpdatedAtUtc = snapshot.UpdatedAtUtc;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
