using System.Collections.Concurrent;
using Formax.Application.Interfaces;

namespace Formax.Infrastructure.Radar.Sources.Scheduling
{
    /// <summary>
    /// Radar Source Engine (R.8.2) — process-local execution lock. Registered as a
    /// singleton. Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>;
    /// TryAdd is the atomic acquire.
    /// </summary>
    public sealed class InMemorySourceExecutionLock : ISourceExecutionLock
    {
        private readonly ConcurrentDictionary<string, byte> _locked = new();

        public bool TryAcquire(string sourceKey) => _locked.TryAdd(sourceKey, 1);

        public void Release(string sourceKey) => _locked.TryRemove(sourceKey, out _);

        public bool IsLocked(string sourceKey) => _locked.ContainsKey(sourceKey);
    }
}
