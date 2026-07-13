using System;
using System.Collections.Concurrent;
using Formax.Application.Interfaces;

namespace Formax.Infrastructure.Radar.Sources.Scheduling
{
    /// <summary>
    /// Radar Source Engine (R.8.2) — process-local last-run store. Registered as a
    /// singleton so values survive across scheduler cycles (but not restarts; durable
    /// persistence arrives in a later sprint behind the same interface).
    /// </summary>
    public sealed class InMemorySourceScheduleTracker : ISourceScheduleTracker
    {
        private readonly ConcurrentDictionary<string, DateTime> _lastRun = new();

        public DateTime? GetLastRun(string sourceKey)
            => _lastRun.TryGetValue(sourceKey, out var t) ? t : null;

        public void SetLastRun(string sourceKey, DateTime runAtUtc)
            => _lastRun[sourceKey] = runAtUtc;
    }
}
