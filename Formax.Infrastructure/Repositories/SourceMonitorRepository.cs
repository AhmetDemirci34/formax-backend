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
    /// Radar Source Engine (R.8.7) — EF Core persistence for monitor snapshots.
    /// </summary>
    public sealed class SourceMonitorRepository : ISourceMonitorRepository
    {
        private readonly FormaxDbContext _context;

        public SourceMonitorRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<SourceMonitorSnapshot?> GetBySourceIdAsync(int sourceId, CancellationToken ct = default)
            => await _context.SourceMonitorSnapshots
                .FirstOrDefaultAsync(x => x.SourceId == sourceId, ct);

        public async Task<IReadOnlyList<SourceMonitorSnapshot>> GetAllAsync(CancellationToken ct = default)
            => await _context.SourceMonitorSnapshots.AsNoTracking().ToListAsync(ct);

        public async Task UpsertAsync(SourceMonitorSnapshot snapshot, CancellationToken ct = default)
        {
            var existing = await _context.SourceMonitorSnapshots
                .FirstOrDefaultAsync(x => x.SourceId == snapshot.SourceId, ct);

            if (existing is null)
            {
                _context.SourceMonitorSnapshots.Add(snapshot);
                return;
            }

            existing.SourceKey = snapshot.SourceKey;
            existing.Status = snapshot.Status;
            existing.AlertType = snapshot.AlertType;
            existing.Reason = snapshot.Reason;
            existing.HealthScoreAtEval = snapshot.HealthScoreAtEval;
            existing.ConsecutiveFailuresAtEval = snapshot.ConsecutiveFailuresAtEval;
            existing.LastSuccessAtUtc = snapshot.LastSuccessAtUtc;
            existing.MinutesSinceLastSuccess = snapshot.MinutesSinceLastSuccess;
            existing.EvaluatedAtUtc = snapshot.EvaluatedAtUtc;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
