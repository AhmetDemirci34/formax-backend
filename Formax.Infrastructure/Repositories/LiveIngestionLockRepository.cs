using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// DB-backed distributed lock for LiveMatchIngestionJob.
    ///
    /// Acquire uses an atomic SQL UPDATE with a WHERE predicate so only one
    /// instance wins the race — no SELECT-then-UPDATE pattern.
    ///
    /// Bootstrap: the singleton row (Id=1) is created on first acquire if it
    /// does not yet exist, with a DbUpdateException race guard.
    /// </summary>
    public class LiveIngestionLockRepository : ILiveIngestionLockRepository
    {
        private readonly FormaxDbContext _context;

        public LiveIngestionLockRepository(FormaxDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<bool> TryAcquireAsync(
            string instanceId,
            TimeSpan stalenessThreshold,
            CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var staleCutoff = now - stalenessThreshold;

            // Atomic acquire: succeeds when row is free, re-entrant, or stale.
            // ExecuteSqlInterpolatedAsync uses parameterized queries — no injection risk.
            int rows = await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE LiveIngestionLocks
                   SET    OwnerInstanceId = {instanceId},
                          AcquiredAt      = {now},
                          HeartbeatAt     = {now}
                   WHERE  Id = 1
                     AND  (    OwnerInstanceId = ''
                            OR OwnerInstanceId = {instanceId}
                            OR HeartbeatAt     < {staleCutoff} )",
                ct);

            if (rows == 1) return true;

            // Row may not exist yet (first startup).  Try INSERT then re-acquire.
            var exists = await _context.LiveIngestionLocks
                .AnyAsync(x => x.Id == 1, ct);

            if (!exists)
            {
                try
                {
                    _context.LiveIngestionLocks.Add(new LiveIngestionLock
                    {
                        Id = 1,
                        OwnerInstanceId = instanceId,
                        AcquiredAt = now,
                        HeartbeatAt = now
                    });
                    await _context.SaveChangesAsync(ct);
                    return true;
                }
                catch (DbUpdateException)
                {
                    // Another instance inserted the row first — re-try the atomic UPDATE.
                    _context.ChangeTracker.Clear();

                    rows = await _context.Database.ExecuteSqlInterpolatedAsync(
                        $@"UPDATE LiveIngestionLocks
                           SET    OwnerInstanceId = {instanceId},
                                  AcquiredAt      = {now},
                                  HeartbeatAt     = {now}
                           WHERE  Id = 1
                             AND  (    OwnerInstanceId = ''
                                    OR OwnerInstanceId = {instanceId}
                                    OR HeartbeatAt     < {staleCutoff} )",
                        ct);

                    return rows == 1;
                }
            }

            // Row exists; another instance actively holds the lock.
            return false;
        }

        /// <inheritdoc />
        public async Task HeartbeatAsync(string instanceId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE LiveIngestionLocks
                   SET    HeartbeatAt = {now}
                   WHERE  Id = 1
                     AND  OwnerInstanceId = {instanceId}",
                ct);
        }

        /// <inheritdoc />
        public async Task ReleaseAsync(string instanceId, CancellationToken ct = default)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE LiveIngestionLocks
                   SET    OwnerInstanceId = ''
                   WHERE  Id = 1
                     AND  OwnerInstanceId = {instanceId}",
                ct);
        }
    }
}
