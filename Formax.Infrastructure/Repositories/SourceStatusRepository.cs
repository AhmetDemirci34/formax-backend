using System;
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
    /// Radar Source Engine (R.8.1) — EF Core persistence for per-source runtime status.
    /// </summary>
    public sealed class SourceStatusRepository : ISourceStatusRepository
    {
        private readonly FormaxDbContext _context;

        public SourceStatusRepository(FormaxDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<SourceStatus>> GetAllAsync(CancellationToken ct = default)
            => await _context.SourceStatuses.AsNoTracking().ToListAsync(ct);

        public async Task<SourceStatus?> GetBySourceIdAsync(int sourceId, CancellationToken ct = default)
            => await _context.SourceStatuses
                .FirstOrDefaultAsync(x => x.SourceId == sourceId, ct);

        public async Task<SourceStatus> UpsertAsync(SourceStatus status, CancellationToken ct = default)
        {
            var existing = await _context.SourceStatuses
                .FirstOrDefaultAsync(x => x.SourceId == status.SourceId, ct);

            if (existing is null)
            {
                _context.SourceStatuses.Add(status);
                return status;
            }

            existing.Active = status.Active;
            existing.LastSuccessUtc = status.LastSuccessUtc;
            existing.LastFailureUtc = status.LastFailureUtc;
            existing.DataFreshnessAtUtc = status.DataFreshnessAtUtc;
            existing.LastNormalizeError = status.LastNormalizeError;
            existing.SuccessCount = status.SuccessCount;
            existing.ErrorCount = status.ErrorCount;
            existing.TimeoutCount = status.TimeoutCount;
            existing.ConsecutiveFailures = status.ConsecutiveFailures;
            existing.SuccessRate = status.SuccessRate;
            existing.DuplicateRate = status.DuplicateRate;
            existing.AvgDurationMs = status.AvgDurationMs;
            existing.HealthScore = status.HealthScore;
            existing.LastVolume = status.LastVolume;
            existing.ExpectedVolume = status.ExpectedVolume;
            existing.CircuitState = status.CircuitState;
            existing.CircuitOpenedAtUtc = status.CircuitOpenedAtUtc;
            existing.UpdatedAtUtc = status.UpdatedAtUtc;
            return existing;
        }

        public async Task<SourceStatus> EnsureExistsAsync(int sourceId, CancellationToken ct = default)
        {
            var existing = await _context.SourceStatuses
                .FirstOrDefaultAsync(x => x.SourceId == sourceId, ct);

            if (existing is not null) return existing;

            var fresh = new SourceStatus
            {
                SourceId = sourceId,
                Active = true,
                CircuitState = Domain.Enums.SourceCircuitState.Closed,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _context.SourceStatuses.Add(fresh);
            return fresh;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
