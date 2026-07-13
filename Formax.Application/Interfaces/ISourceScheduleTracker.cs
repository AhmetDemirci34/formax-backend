using System;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Radar Source Engine (R.8.2) — last-run bookkeeping used to decide whether a
    /// source's interval has elapsed. In-memory for this sprint (no collection yet,
    /// so nothing meaningful to persist); the abstraction lets later sprints swap in
    /// a durable store without touching the scheduler.
    /// </summary>
    public interface ISourceScheduleTracker
    {
        /// <summary>Last time the source was dispatched, or null if never.</summary>
        DateTime? GetLastRun(string sourceKey);

        /// <summary>Record that the source was dispatched at the given UTC time.</summary>
        void SetLastRun(string sourceKey, DateTime runAtUtc);
    }
}
