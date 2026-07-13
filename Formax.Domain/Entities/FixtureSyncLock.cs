namespace Formax.Domain.Entities
{
    /// <summary>
    /// Singleton lock row (Id = 1) used by FixtureSyncJob for distributed
    /// leader election.  Only the instance that owns this row runs fixture sync;
    /// all other instances skip their cycle and wait.
    ///
    /// Staleness rule: if HeartbeatAt is older than LockStaleness (30 min) the
    /// lock is considered abandoned and any waiting instance may take it.
    /// </summary>
    public class FixtureSyncLock
    {
        public int Id { get; set; }

        /// <summary>
        /// Identity of the owning process, e.g. "hostname:pid:guid".
        /// Empty string means the lock is free.
        /// </summary>
        public string OwnerInstanceId { get; set; } = string.Empty;

        /// <summary>UTC time the current owner acquired the lock.</summary>
        public DateTime AcquiredAt { get; set; }

        /// <summary>
        /// UTC time of the last heartbeat.  Updated after each successful cycle.
        /// Used to detect abandoned locks when a process dies without releasing.
        /// </summary>
        public DateTime HeartbeatAt { get; set; }
    }
}
