using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.PostMatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// KALICI VİDEO KEŞFİ — kuyruk, defter ve kaynak kataloğu. GET uçları salt okunur. POST uçları normal arka
    /// plan hattını bir tur çalıştırır (aynı kuyruk, aynı kimlik kapısı); DB'ye elle video yazan bir uç YOKTUR.
    /// </summary>
    [ApiController]
    [Route("admin/video-discovery")]
    public sealed class AdminVideoDiscoveryController : ControllerBase
    {
        private readonly FormaxDbContext _db;
        public AdminVideoDiscoveryController(FormaxDbContext db) => _db = db;

        [HttpGet("queue")]
        public async Task<IActionResult> Queue([FromQuery] int? matchId, [FromQuery] int take = 50, CancellationToken ct = default)
        {
            var q = _db.MatchVideoDiscoveryQueue.AsNoTracking();
            if (matchId != null) q = q.Where(x => x.MatchId == matchId);
            var rows = await q.OrderByDescending(x => x.EndUtc).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
            var summary = await _db.MatchVideoDiscoveryQueue.AsNoTracking()
                .GroupBy(x => new { x.State, x.EnqueueReason }).Select(g => new { g.Key.State, g.Key.EnqueueReason, Count = g.Count() })
                .ToListAsync(ct);
            return Ok(new { summary, rows });
        }

        [HttpGet("attempts/{matchId:int}")]
        public async Task<IActionResult> Attempts(int matchId, [FromQuery] int take = 200, CancellationToken ct = default)
            => Ok(await _db.MatchVideoDiscoveryAttempts.AsNoTracking().Where(a => a.MatchId == matchId)
                .OrderByDescending(a => a.AttemptedAtUtc).ThenByDescending(a => a.Accepted).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct));

        [HttpGet("catalog")]
        public async Task<IActionResult> Catalog([FromQuery] string? status, CancellationToken ct = default)
        {
            var q = _db.OfficialVideoSourceCatalog.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
            var rows = await q.OrderBy(r => r.Key).ToListAsync(ct);
            return Ok(new
            {
                total = rows.Count,
                byStatus = rows.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count()),
                byVia = rows.GroupBy(r => r.DiscoveredVia).ToDictionary(g => g.Key, g => g.Count()),
                rows
            });
        }

        [HttpGet("hosts")]
        public IActionResult Hosts([FromServices] HostRateLimiter limiter) => Ok(limiter.Snapshot());

        /// <summary>Katalog keşfini bir tur çalıştırır (Wikidata + resmî site kanıtı).</summary>
        [HttpPost("catalog/discover")]
        public async Task<IActionResult> DiscoverSources([FromServices] OfficialVideoSourceDiscoveryService svc,
            [FromQuery] int maxTeams = 60, CancellationToken ct = default)
            => Ok(await svc.RunAsync(DateTime.UtcNow, Math.Clamp(maxTeams, 1, 400), ct));

        /// <summary>Kuyruğu bir tur çalıştırır (kuyruğa alma + zamanı gelenler).</summary>
        [HttpPost("queue/run")]
        public async Task<IActionResult> RunQueue([FromServices] MatchVideoDiscoveryQueueService svc, CancellationToken ct = default)
            => Ok(await svc.RunCycleAsync(DateTime.UtcNow, ct));
    }
}
