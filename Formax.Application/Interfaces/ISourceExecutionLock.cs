namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Source Engine (R.8.2) — guards against overlapping executions of the
    /// same source. In-memory within a single process for this sprint; a source is
    /// "locked" from when the scheduler picks it up until it releases.
    /// </summary>
    public interface ISourceExecutionLock
    {
        /// <summary>Atomically mark the source as executing. Returns false if it was
        /// already locked.</summary>
        bool TryAcquire(string sourceKey);

        /// <summary>Release the source so a future cycle can pick it up again.</summary>
        void Release(string sourceKey);

        /// <summary>True if the source is currently locked.</summary>
        bool IsLocked(string sourceKey);
    }
}
