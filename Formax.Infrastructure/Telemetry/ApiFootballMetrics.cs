using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Formax.Infrastructure.Telemetry
{
    /// <summary>
    /// Timeline Operations — GERÇEK api-football istek sayacı (process ömrü boyunca, in-memory).
    /// ApiFootballMeteringHandler her giden HTTP isteğinde artırır; sağlayıcı cache-hit'te RecordCacheHit
    /// çağırır. Tahmin YOK — tüm sayılar gerçek çağrılardan. Thread-safe (Interlocked + ConcurrentDictionary).
    /// Salt-ölçüm: hiçbir davranışı etkilemez; yalnız Quota Intelligence dashboard'u okur.
    /// </summary>
    public sealed class ApiFootballMetrics
    {
        public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

        private long _total;
        private long _failed;
        private long _cacheHits;

        private readonly ConcurrentDictionary<string, long> _byEndpoint = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, long> _failedByEndpoint = new(StringComparer.OrdinalIgnoreCase);

        // ── Job attribution (ApiFootballJobAttributionHandler yazar) ───────────────────
        // GERÇEK HTTP denemesi başına sayılır (retry dahil); cache-hit HTTP'ye gitmediği için
        // buraya HİÇ düşmez. _byEndpoint sayacı bağımsızdır ve anlamı değişmemiştir.
        private long _jobTotal;
        private readonly ConcurrentDictionary<string, long> _byJob = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, long> _byJobEndpoint = new(StringComparer.OrdinalIgnoreCase);

        public void RecordRequest(string endpointFamily, bool success)
        {
            Interlocked.Increment(ref _total);
            _byEndpoint.AddOrUpdate(endpointFamily, 1, (_, v) => v + 1);
            if (!success)
            {
                Interlocked.Increment(ref _failed);
                _failedByEndpoint.AddOrUpdate(endpointFamily, 1, (_, v) => v + 1);
            }
        }

        public void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);

        /// <summary>
        /// Job-bazlı gerçek HTTP isteği kaydı. <paramref name="jobName"/>
        /// <see cref="ApiFootballCallScope"/>'tan gelir; ilan edilmemişse "unattributed".
        /// </summary>
        public void RecordJobRequest(string jobName, string endpointFamily)
        {
            var job = string.IsNullOrWhiteSpace(jobName) ? ApiFootballCallScope.Unattributed : jobName;
            var family = string.IsNullOrWhiteSpace(endpointFamily) ? "unknown" : endpointFamily;

            Interlocked.Increment(ref _jobTotal);
            _byJob.AddOrUpdate(job, 1, (_, v) => v + 1);
            _byJobEndpoint.AddOrUpdate($"{job}|{family}", 1, (_, v) => v + 1);
        }

        public ApiFootballMetricsSnapshot Snapshot()
        {
            var total = Interlocked.Read(ref _total);
            var failed = Interlocked.Read(ref _failed);
            var cacheHits = Interlocked.Read(ref _cacheHits);
            var uptimeHours = Math.Max(1.0 / 3600.0, (DateTime.UtcNow - StartedAtUtc).TotalHours);

            var attempts = total + cacheHits; // mantıksal istek talebi = gerçek + cache'ten karşılanan
            return new ApiFootballMetricsSnapshot
            {
                StartedAtUtc          = StartedAtUtc.ToString("u"),
                UptimeHours           = Math.Round(uptimeHours, 3),
                TotalRequests         = total,
                FailedRequests        = failed,
                SuccessRequests       = total - failed,
                CacheHits             = cacheHits,
                CacheGainPercent      = attempts > 0 ? (int)Math.Round(100.0 * cacheHits / attempts) : 0,
                RequestsPerHour       = Math.Round(total / uptimeHours, 2),
                RequestsPerDayProjected = Math.Round(total / uptimeHours * 24.0, 1),
                ByEndpoint            = _byEndpoint.OrderByDescending(kv => kv.Value)
                                                   .ToDictionary(kv => kv.Key, kv => kv.Value),
                FailedByEndpoint      = _failedByEndpoint.OrderByDescending(kv => kv.Value)
                                                   .ToDictionary(kv => kv.Key, kv => kv.Value),
                JobAttributedRequests = Interlocked.Read(ref _jobTotal),
                ByJob                 = _byJob.OrderByDescending(kv => kv.Value)
                                                   .ToDictionary(kv => kv.Key, kv => kv.Value),
                ByJobAndEndpoint      = _byJobEndpoint.OrderByDescending(kv => kv.Value)
                                                   .ToDictionary(kv => kv.Key, kv => kv.Value)
            };
        }
    }

    public sealed class ApiFootballMetricsSnapshot
    {
        public string StartedAtUtc { get; init; } = "";
        public double UptimeHours { get; init; }
        public long TotalRequests { get; init; }
        public long FailedRequests { get; init; }
        public long SuccessRequests { get; init; }
        public long CacheHits { get; init; }
        public int CacheGainPercent { get; init; }
        public double RequestsPerHour { get; init; }
        public double RequestsPerDayProjected { get; init; }
        public Dictionary<string, long> ByEndpoint { get; init; } = new();
        public Dictionary<string, long> FailedByEndpoint { get; init; } = new();

        // ── Job attribution (gerçek HTTP denemesi başına; retry dahil, cache-hit hariç) ──
        public long JobAttributedRequests { get; init; }
        public Dictionary<string, long> ByJob { get; init; } = new();
        /// <summary>Anahtar biçimi: "{job}|{endpointFamily}".</summary>
        public Dictionary<string, long> ByJobAndEndpoint { get; init; } = new();
    }
}
