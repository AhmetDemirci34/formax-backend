using System.Linq;
using Formax.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// SÜREÇ SAĞLIĞI — son dakikaların runtime örnekleri, takılma anları ve /detail gecikme dağılımı.
    /// Salt okunur; hiçbir dış kaynağa çıkmaz.
    /// </summary>
    [ApiController]
    [Route("admin/diagnostics")]
    public sealed class AdminDiagnosticsController : ControllerBase
    {
        private readonly RuntimeHealthMonitor _monitor;
        public AdminDiagnosticsController(RuntimeHealthMonitor monitor) => _monitor = monitor;

        [HttpGet("runtime")]
        public IActionResult Runtime([FromQuery] int minutes = 15) => Ok(_monitor.Snapshot(minutes));

        /// <summary>/detail zamanlayıcısının sınırlı kuyruk metrikleri (doygunluk, ret, kapanışta iptal).</summary>
        [HttpGet("scheduler")]
        public IActionResult Scheduler([FromServices] Formax.Application.Interfaces.IBlockingWorkScheduler scheduler)
            => scheduler is Formax.Infrastructure.Concurrency.DedicatedThreadWorkScheduler s
                ? Ok(new
                {
                    s.WorkerCount, s.Capacity, s.Queued, s.PeakQueued, s.Running, s.Completed, s.Rejected, s.CanceledOnShutdown,
                    s.Saturation, s.MaxQueueWaitMs,
                    threadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count,
                    threadPoolThreads = System.Threading.ThreadPool.ThreadCount,
                    pendingWorkItems = System.Threading.ThreadPool.PendingWorkItemCount
                })
                : Ok(new { type = scheduler.GetType().Name });

        /// <summary>
        /// GİDEN HTTP KAYDI — süreç içindeki bütün HttpClient istekleri; <c>inbound</c> dolu olanlar bir kullanıcı isteğinin
        /// yolunda çıkmıştır. <c>inboundPrefix</c> ile süzülür (ör. "/api/matches/"). Host + yol; sorgu dizesi yok.
        /// </summary>
        [HttpGet("outbound")]
        public IActionResult Outbound([FromServices] OutboundHttpObserver observer, [FromQuery] DateTime? sinceUtc,
            [FromQuery] string? inboundPrefix, [FromQuery] int take = 200)
        {
            var since = sinceUtc?.ToUniversalTime() ?? System.DateTime.UtcNow.AddMinutes(-15);
            var rows = observer.Since(since);
            var inbound = rows.Where(r => r.InboundPath != null
                                          && (inboundPrefix == null || r.InboundPath.StartsWith(inboundPrefix, System.StringComparison.OrdinalIgnoreCase))).ToList();
            return Ok(new
            {
                sinceUtc = since, total = rows.Count, background = rows.Count(r => r.Background), onRequestPath = inbound.Count,
                onRequestPathByHost = inbound.GroupBy(r => r.Host).ToDictionary(g => g.Key, g => g.Count()),
                backgroundByHost = rows.Where(r => r.Background).GroupBy(r => r.Host).OrderByDescending(g => g.Count()).Take(40).ToDictionary(g => g.Key, g => g.Count()),
                requestPathSamples = inbound.Take(System.Math.Clamp(take, 1, 2000))
            });
        }
    }
}
