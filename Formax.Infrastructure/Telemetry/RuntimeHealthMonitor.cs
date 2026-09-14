using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Telemetry
{
    /// <summary>Bir saniyelik süreç sağlığı ölçümü.</summary>
    public sealed record RuntimeSample(
        DateTime Utc,
        int OversleepMs,          // özel iş parçacığının 1 sn uykusu ne kadar gecikti (süreç çapında donma)
        int PoolProbeMs,          // thread pool'a atılan boş işin başlama gecikmesi (pool açlığı)
        int PoolThreads,
        long PoolPending,
        int GcPauseMs,            // son örnekten beri GC duraklaması
        int Gen2Delta,
        double CpuPct,
        long OpenDbConnections,
        long OpenDbReaders,
        long SlowDbCommands,
        IReadOnlyDictionary<string, double> Counters);

    /// <summary>/detail isteğinin aşama ölçümü.</summary>
    public sealed record DetailTiming(
        DateTime Utc, string CorrelationId, int MatchId, int Status, int TotalMs, int HandlerMs,
        int SerializeMs, long DbCommands, int DbMs, int DbMaxMs, long ConnOpens, int ConnWaitMs, int ConnWaitMaxMs,
        string? SlowestSql, bool Aborted);

    /// <summary>
    /// SÜREÇ SAĞLIĞI İZLEYİCİSİ — dış araç (dotnet-counters) KURMADAN, runtime'ın kendi EventCounter'larını
    /// süreç içinden okur ve her saniye örnekler.
    ///
    /// Ayırıcı ölçümler:
    ///  • Özel (pool dışı) iş parçacığının uyku sapması büyükse bütün süreç donmuştur (GC askıya alma,
    ///    sayfalama, hata ayıklayıcı).
    ///  • Sapma küçük ama pool probu gecikiyorsa ThreadPool açlığıdır (sync-over-async, bloklayan I/O).
    ///  • İkisi de temizken DB bağlantı beklemesi yüksekse SqlClient havuzu/sunucu tarafıdır.
    /// Anormal an "[RUNTIME-STALL]" olarak loglanır; son 30 dk bellekte tutulur (/admin/diagnostics/runtime).
    /// </summary>
    public sealed class RuntimeHealthMonitor : IHostedService, IDisposable
    {
        private const int Capacity = 1800;
        private static readonly TimeSpan StallThreshold = TimeSpan.FromMilliseconds(1000);

        private readonly ILogger<RuntimeHealthMonitor> _log;
        private readonly object _gate = new();
        private readonly Queue<RuntimeSample> _samples = new();
        private readonly Queue<RuntimeSample> _stalls = new();
        private readonly Queue<DetailTiming> _details = new();
        private CounterListener? _listener;
        private Thread? _thread;
        private volatile bool _stop;
        private long _probeQueuedTicks;     // 0 = prob beklemede değil
        private long _lastProbeLatencyMs;

        public RuntimeHealthMonitor(ILogger<RuntimeHealthMonitor> log) => _log = log;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _listener = new CounterListener();
            _thread = new Thread(Loop) { IsBackground = true, Name = "formax-runtime-monitor", Priority = ThreadPriority.AboveNormal };
            _thread.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) { _stop = true; return Task.CompletedTask; }

        public void Dispose() { _stop = true; _listener?.Dispose(); }

        public void RecordDetail(DetailTiming timing)
        {
            lock (_gate) { _details.Enqueue(timing); while (_details.Count > 2000) _details.Dequeue(); }
        }

        public object Snapshot(int minutes)
        {
            var since = DateTime.UtcNow.AddMinutes(-Math.Clamp(minutes, 1, 30));
            lock (_gate)
            {
                var details = _details.Where(d => d.Utc >= since).ToList();
                var ms = details.Select(d => d.TotalMs).OrderBy(x => x).ToList();
                int P(double q) => ms.Count == 0 ? 0 : ms[Math.Min(ms.Count - 1, (int)Math.Ceiling(q * ms.Count) - 1)];
                return new
                {
                    windowMinutes = minutes,
                    samples = _samples.Where(s => s.Utc >= since).ToList(),
                    stalls = _stalls.Where(s => s.Utc >= since).ToList(),
                    detail = new
                    {
                        count = ms.Count, p50 = P(0.50), p95 = P(0.95), p99 = P(0.99), max = ms.Count == 0 ? 0 : ms[^1],
                        over10s = ms.Count(x => x > 10_000), errors = details.Count(d => d.Status >= 500 || d.Aborted),
                        slowest = details.OrderByDescending(d => d.TotalMs).Take(10).ToList()
                    }
                };
            }
        }

        private void Loop()
        {
            var proc = Process.GetCurrentProcess();
            var lastCpu = proc.TotalProcessorTime;
            var lastPause = GC.GetTotalPauseDuration();
            var lastGen2 = GC.CollectionCount(2);
            var inStall = false;

            while (!_stop)
            {
                var sw = Stopwatch.StartNew();
                QueueProbe();
                Thread.Sleep(1000);
                var elapsed = sw.Elapsed;

                proc.Refresh();
                var cpu = proc.TotalProcessorTime;
                var pause = GC.GetTotalPauseDuration();
                var gen2 = GC.CollectionCount(2);

                var queued = Interlocked.Read(ref _probeQueuedTicks);
                var probeMs = queued != 0
                    ? (int)((Stopwatch.GetTimestamp() - queued) * 1000 / Stopwatch.Frequency)   // hâlâ başlamadı
                    : (int)Interlocked.Read(ref _lastProbeLatencyMs);

                var sample = new RuntimeSample(
                    DateTime.UtcNow,
                    (int)Math.Max(0, (elapsed - TimeSpan.FromSeconds(1)).TotalMilliseconds),
                    probeMs,
                    ThreadPool.ThreadCount,
                    ThreadPool.PendingWorkItemCount,
                    (int)(pause - lastPause).TotalMilliseconds,
                    gen2 - lastGen2,
                    Math.Round((cpu - lastCpu).TotalMilliseconds / elapsed.TotalMilliseconds * 100 / Environment.ProcessorCount, 1),
                    DbTimingInterceptor.OpenConnections,
                    DbTimingInterceptor.OpenReaders,
                    DbTimingInterceptor.SlowCommands,
                    _listener?.Latest() ?? new Dictionary<string, double>());
                lastCpu = cpu; lastPause = pause; lastGen2 = gen2;

                var stall = sample.OversleepMs > StallThreshold.TotalMilliseconds
                            || sample.PoolProbeMs > StallThreshold.TotalMilliseconds
                            || sample.GcPauseMs > StallThreshold.TotalMilliseconds;
                lock (_gate)
                {
                    _samples.Enqueue(sample);
                    while (_samples.Count > Capacity) _samples.Dequeue();
                    if (stall) { _stalls.Enqueue(sample); while (_stalls.Count > 300) _stalls.Dequeue(); }
                }
                if (stall && !inStall)
                    _log.LogWarning(
                        "[RUNTIME-STALL] oversleep={Over}ms poolProbe={Probe}ms poolThreads={Threads} pending={Pending} gcPause={Gc}ms cpu={Cpu}% openConn={Conn} openReaders={Readers} counters={Counters}",
                        sample.OversleepMs, sample.PoolProbeMs, sample.PoolThreads, sample.PoolPending, sample.GcPauseMs,
                        sample.CpuPct, sample.OpenDbConnections, sample.OpenDbReaders,
                        string.Join(",", sample.Counters.Select(kv => kv.Key + "=" + kv.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))));
                inStall = stall;
            }
        }

        private void QueueProbe()
        {
            if (Interlocked.CompareExchange(ref _probeQueuedTicks, Stopwatch.GetTimestamp(), 0) != 0) return;
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                var queued = Interlocked.Exchange(ref _probeQueuedTicks, 0);
                if (queued != 0)
                    Interlocked.Exchange(ref _lastProbeLatencyMs, (Stopwatch.GetTimestamp() - queued) * 1000 / Stopwatch.Frequency);
            }, null);
        }

        /// <summary>System.Runtime + Microsoft.Data.SqlClient EventCounter'ları (1 sn aralık).</summary>
        private sealed class CounterListener : EventListener
        {
            private static readonly HashSet<string> Wanted = new(StringComparer.Ordinal)
            {
                "threadpool-queue-length", "threadpool-thread-count", "monitor-lock-contention-count", "time-in-gc",
                "alloc-rate", "gen-2-gc-count", "active-hard-connections", "active-soft-connects",
                "number-of-pooled-connections", "number-of-free-connections", "number-of-stasis-connections",
                "number-of-active-connections", "number-of-inactive-connection-pools"
            };
            private readonly Dictionary<string, double> _latest = new();

            public Dictionary<string, double> Latest() { lock (_latest) return new Dictionary<string, double>(_latest); }

            protected override void OnEventSourceCreated(EventSource source)
            {
                if (source.Name is "System.Runtime" or "Microsoft.Data.SqlClient.EventSource")
                    EnableEvents(source, EventLevel.Informational, EventKeywords.All,
                        new Dictionary<string, string?> { ["EventCounterIntervalSec"] = "1" });
            }

            protected override void OnEventWritten(EventWrittenEventArgs e)
            {
                if (e.EventName != "EventCounters" || e.Payload is not { Count: > 0 } p || p[0] is not IDictionary<string, object> data) return;
                if (!data.TryGetValue("Name", out var n) || n is not string name || !Wanted.Contains(name)) return;
                double v = data.TryGetValue("Mean", out var mean) ? Convert.ToDouble(mean)
                         : data.TryGetValue("Increment", out var inc) ? Convert.ToDouble(inc) : double.NaN;
                if (double.IsNaN(v)) return;
                lock (_latest) _latest[name] = v;
            }
        }
    }
}
