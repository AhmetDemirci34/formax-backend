using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Http;
using Formax.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// FORMAX veri katmanı kota panosu — SALT OKUMA, api-football'a HİÇ çıkmaz.
    /// Günlük gerçek istek sayısı kalıcı depodan (süreç yeniden başlasa da doğru) okunur;
    /// cache isabetleri süreç ömrü sayaçlarından gelir.
    /// </summary>
    [ApiController]
    [Route("admin/api-football")]
    public class AdminApiFootballUsageController : ControllerBase
    {
        private readonly ApiFootballMetrics _metrics;
        private readonly ApiFootballHttpCacheStore _store;
        private readonly IConfiguration _config;

        public AdminApiFootballUsageController(
            ApiFootballMetrics metrics, ApiFootballHttpCacheStore store, IConfiguration config)
        {
            _metrics = metrics; _store = store; _config = config;
        }

        [HttpGet("usage")]
        public async Task<IActionResult> Usage(CancellationToken ct)
        {
            var snap = _metrics.Snapshot();
            var limit = _config.GetValue("ApiFootball:DailyRequestLimit", 100);
            var used = await _store.GetDailyUsageAsync(ct);

            return Ok(new
            {
                dailyLimit = limit,
                realRequestsToday = used,
                remaining = used < 0 ? (int?)null : (limit - used < 0 ? 0 : limit - used),
                criticalReserve = _config.GetValue("ApiFootball:CriticalReserve", 20),
                memoryCacheHits = snap.CacheHits,
                persistentCacheHits = snap.PersistentCacheHits,
                budgetBlockedRequests = snap.BudgetBlockedRequests,
                budgetBlockedByEndpoint = snap.BudgetBlockedByEndpoint,
                processRealRequests = snap.TotalRequests,
                processFailedRequests = snap.FailedRequests,
                byEndpoint = snap.ByEndpoint,
                byCaller = snap.ByJob,
                byCallerAndEndpoint = snap.ByJobAndEndpoint,
                processStartedUtc = snap.StartedAtUtc
            });
        }
    }
}
