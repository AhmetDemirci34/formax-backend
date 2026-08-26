using System;
using System.Threading;

namespace Formax.Infrastructure.Telemetry
{
    /// <summary>
    /// Timeline Operations — son senkron cycle'ının GERÇEK istatistikleri (in-memory, process ömrü).
    /// HistoricalSyncJob her cycle sonunda RecordCycle çağırır. Dashboard "son güncelleme zamanı",
    /// "son senkron süresi", "başarısız/eklenen" değerlerini buradan okur. Tahmin YOK.
    /// </summary>
    public sealed class TimelineSyncTelemetry
    {
        private readonly object _lock = new();

        public DateTime? LastCycleAtUtc { get; private set; }
        public long LastCycleDurationMs { get; private set; }
        public int LastCycleTeams { get; private set; }
        public int LastCycleMatchesAdded { get; private set; }
        public int LastCycleTeamsWithNoData { get; private set; }
        public long LastCycleApiRequests { get; private set; }
        public string LastCycleTrigger { get; private set; } = "";

        private long _totalCycles;
        private long _totalTeamsProcessed;
        private long _totalMatchesAdded;

        public long TotalCyclesRun => Interlocked.Read(ref _totalCycles);
        public long TotalTeamsProcessed => Interlocked.Read(ref _totalTeamsProcessed);
        public long TotalMatchesAdded => Interlocked.Read(ref _totalMatchesAdded);

        public void RecordCycle(string trigger, long durationMs, int teams, int matchesAdded,
            int teamsWithNoData, long apiRequests)
        {
            lock (_lock)
            {
                LastCycleAtUtc = DateTime.UtcNow;
                LastCycleDurationMs = durationMs;
                LastCycleTeams = teams;
                LastCycleMatchesAdded = matchesAdded;
                LastCycleTeamsWithNoData = teamsWithNoData;
                LastCycleApiRequests = apiRequests;
                LastCycleTrigger = trigger;
            }
            Interlocked.Increment(ref _totalCycles);
            Interlocked.Add(ref _totalTeamsProcessed, teams);
            Interlocked.Add(ref _totalMatchesAdded, matchesAdded);
        }
    }
}
