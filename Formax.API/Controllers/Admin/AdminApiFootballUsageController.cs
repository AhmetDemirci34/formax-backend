using System.Linq;
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

        /// <summary>
        /// İSTEK BAŞINA DÖKÜM — SALT OKUNUR, sıfır dış istek. Her satır: zaman, çağıran job,
        /// uç, güvenli sorgu, fikstür, cache (L1/L2/Miss), bütçe kararı, HTTP ve sağlayıcı
        /// sonucu. Sır/anahtar içermez. <c>?realOnly=true</c> yalnız gerçek HTTP'yi döker.
        /// </summary>
        [HttpGet("requests")]
        public IActionResult Requests([FromServices] ApiFootballRequestLog log, [FromQuery] bool realOnly = false)
        {
            var rows = log.Snapshot();
            var shown = realOnly ? rows.Where(r => r.RealRequest).ToList() : rows.ToList();
            return Ok(new
            {
                totalRecordedSinceStart = log.Total,
                realRequests = rows.Count(r => r.RealRequest),
                byCallerAndEndpoint = rows.Where(r => r.RealRequest)
                    .GroupBy(r => r.Caller + "|" + r.Endpoint)
                    .ToDictionary(g => g.Key, g => g.Count()),
                byCache = rows.GroupBy(r => r.Cache).ToDictionary(g => g.Key, g => g.Count()),
                byBudget = rows.GroupBy(r => r.Budget).ToDictionary(g => g.Key, g => g.Count()),
                requests = shown
            });
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
