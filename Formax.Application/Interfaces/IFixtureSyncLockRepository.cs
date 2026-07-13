namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Distributed leader-election lock for FixtureSyncJob.
    /// Backed by a single-row DB table; uses atomic SQL UPDATE for acquire
    /// so there is no TOCTOU race between instances.
    /// </summary>
    public interface IFixtureSyncLockRepository
    {
        /// <summary>
        /// Atomically acquire the singleton lock.
        /// Succeeds when:
        ///   - the lock row is free (OwnerInstanceId == ""), or
        ///   - this instance already owns it (re-entrant), or
        ///   - the lock is stale (HeartbeatAt older than stalenessThreshold).
        /// Returns true if this instance is now the owner.
        /// </summary>
        Task<bool> TryAcquireAsync(
            string instanceId,
            TimeSpan stalenessThreshold,
            CancellationToken ct = default);

        /// <summary>
        /// Update HeartbeatAt to prevent the lock being considered stale.
        /// Call after each successful sync cycle.
        /// </summary>
        Task HeartbeatAsync(string instanceId, CancellationToken ct = default);

        /// <summary>
        /// Release the lock (set OwnerInstanceId to "").
        /// Only affects the row if the caller is the current owner.
        /// Call on graceful shutdown or unrecoverable error.
        /// </summary>
        Task ReleaseAsync(string instanceId, CancellationToken ct = default);
    }
}
