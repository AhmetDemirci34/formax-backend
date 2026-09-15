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
        /// ÖZELLİK DURUMU — video özelliği kapalı mı, host'ta video işi kayıtlı mı, süreçte video dış isteği çıktı mı.
        /// Hosted service listesi çalışan sürecin kendisinden okunur (yapılandırma bayrağından değil).
        /// </summary>
        [HttpGet("features")]
        public IActionResult Features([FromServices] OutboundHttpObserver observer,
            [FromServices] System.Collections.Generic.IEnumerable<Microsoft.Extensions.Hosting.IHostedService> hosted)
        {
            var names = hosted.Select(h => h.GetType().Name).ToList();
            var videoJobs = names.Where(n => n.Contains("Video", System.StringComparison.OrdinalIgnoreCase)
                                             || n.Contains("YouTube", System.StringComparison.OrdinalIgnoreCase)
                                             || n.Contains("OEmbed", System.StringComparison.OrdinalIgnoreCase)
                                             || n == "OfficialWebFeedCrawlJob").ToList();
            var since = System.DateTime.UtcNow.AddHours(-24);
            var recent = observer.Since(since);
            return Ok(new
            {
                VideoFeatureEnabled = false,
                activeVideoJobCount = videoJobs.Count,
                activeVideoJobs = videoJobs,
                videoOutboundRequestsSinceStart = observer.VideoRequestCount,
                lastVideoOutboundRequest = observer.LastVideoRequest,
                youtubeRequestsLast24h = recent.Count(r => r.Host.Contains("youtube", System.StringComparison.OrdinalIgnoreCase) || r.Host.Contains("ytimg", System.StringComparison.OrdinalIgnoreCase)),
                oembedRequestsLast24h = recent.Count(r => r.Path.Contains("oembed", System.StringComparison.OrdinalIgnoreCase)),
                videoSitemapRequestsLast24h = recent.Count(r => OutboundHttpObserver.IsVideoRequest(r.Host, r.Path) && r.Path.Contains("sitemap", System.StringComparison.OrdinalIgnoreCase)),
                outboundTrackedSinceStart = observer.Total,
                hostedServices = names
            });
        }

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
