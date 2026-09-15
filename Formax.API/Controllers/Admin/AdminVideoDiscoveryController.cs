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
            [FromQuery] int maxTeams = 60, [FromQuery] int candidateRecheckHours = 6, CancellationToken ct = default)
            => Ok(await svc.RunAsync(DateTime.UtcNow, Math.Clamp(maxTeams, 1, 400), ct, Math.Clamp(candidateRecheckHours, 0, 48)));

        /// <summary>Kuyruğu bir tur çalıştırır (kuyruğa alma + zamanı gelenler).</summary>
        [HttpPost("queue/run")]
        public async Task<IActionResult> RunQueue([FromServices] MatchVideoDiscoveryQueueService svc, CancellationToken ct = default)
            => Ok(await svc.RunCycleAsync(DateTime.UtcNow, ct));

        /// <summary>
        /// KAPSAM ÖLÇÜMÜ — kilitli 11 organizasyondaki bütün sonuçlanmış maçlar: videolu/videosuz, yaş kovası, kuyruk durumu,
        /// kalıcı imleç. Salt okunur, tek SQL turu (tablo belleğe alınmaz).
        /// </summary>
        [HttpGet("coverage")]
        public async Task<IActionResult> Coverage(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var locked = Formax.Domain.Constants.LockedCompetitions.All.ToList();
            var todayIst = MatchVideoDiscoveryQueueService.IstanbulDay(now);
            var finished = _db.Matches.AsNoTracking().Where(m => m.Status == Formax.Domain.Constants.MatchStatuses.Finished
                                                               && locked.Contains(m.LeagueId) && m.MatchDate <= now);
            var web = Formax.Domain.Entities.MatchVideoRules.OfficialWebProvenance;
            var total = await finished.CountAsync(ct);
            var withFull = await finished.CountAsync(m => _db.MatchVideos.Any(v => v.MatchId == m.Id && v.CanPlayInApp && v.DiscoveryProvenance == web
                && (v.VideoType == "MatchHighlights" || v.VideoType == "ExtendedHighlights")), ct);
            var goalsOnly = await finished.CountAsync(m => _db.MatchVideos.Any(v => v.MatchId == m.Id && v.CanPlayInApp && v.DiscoveryProvenance == web && v.VideoType == "Goal")
                && !_db.MatchVideos.Any(v => v.MatchId == m.Id && v.CanPlayInApp && v.DiscoveryProvenance == web && (v.VideoType == "MatchHighlights" || v.VideoType == "ExtendedHighlights")), ct);
            var blockedOnly = await finished.CountAsync(m => _db.MatchVideos.Any(v => v.MatchId == m.Id && (v.VerificationStatus == "SourceBlocked" || v.VerificationStatus == "Rejected" || v.VerificationStatus == "EmbedBlocked"))
                && !_db.MatchVideos.Any(v => v.MatchId == m.Id && v.CanPlayInApp && v.DiscoveryProvenance == web), ct);
            var queue = await _db.MatchVideoDiscoveryQueue.AsNoTracking().GroupBy(q => new { q.State, q.EnqueueReason })
                .Select(g => new { g.Key.State, g.Key.EnqueueReason, Count = g.Count() }).ToListAsync(ct);
            var queuedTotal = await _db.MatchVideoDiscoveryQueue.CountAsync(ct);
            var scanned = await _db.MatchVideoDiscoveryQueue.CountAsync(q => q.LastAttemptUtc != null, ct);
            var technical = await _db.MatchVideoDiscoveryQueue.CountAsync(q => q.State == "Failed", ct);
            var cursors = await _db.VideoDiscoveryCursors.AsNoTracking().ToListAsync(ct);
            var ends = await finished.Select(m => m.MatchDate).ToListAsync(ct);
            var buckets = ends.GroupBy(d => MatchVideoDiscoveryQueueService.Bucket(Formax.Application.Services.PostMatch.MatchVideoIdentityValidator.EndOf(d), now))
                .ToDictionary(g => g.Key, g => g.Count());
            var videos = await _db.MatchVideos.AsNoTracking().GroupBy(v => new { v.VerificationStatus, v.RejectionReason, v.DiscoveryProvenance })
                .Select(g => new { g.Key.VerificationStatus, g.Key.RejectionReason, g.Key.DiscoveryProvenance, Count = g.Count() }).ToListAsync(ct);
            return Ok(new
            {
                nowUtc = now, todayIstanbul = todayIst.ToString("yyyy-MM-dd"),
                finishedTotal = total, withFullHighlights = withFull, goalClipsOnly = goalsOnly, withoutVideo = total - withFull - goalsOnly,
                wrongOrBlockedOnly = blockedOnly, buckets, queuedTotal, scannedTotal = scanned, technicalRetry = technical, queue, cursors, videos
            });
        }

        [HttpGet("feeds")]
        public async Task<IActionResult> Feeds([FromQuery] string? sourceKey, CancellationToken ct = default)
        {
            var q = _db.OfficialWebFeeds.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(sourceKey)) q = q.Where(f => f.SourceKey == sourceKey);
            var rows = await q.OrderBy(f => f.SourceKey).ThenBy(f => f.Url).ToListAsync(ct);
            var entries = await _db.OfficialWebVideoEntries.AsNoTracking().GroupBy(e => e.SourceKey)
                .Select(g => new { SourceKey = g.Key, Entries = g.Count(), WithYouTube = g.Count(x => x.YouTubeVideoId != null) }).ToListAsync(ct);
            return Ok(new
            {
                total = rows.Count, active = rows.Count(r => r.IsActive), byKind = rows.GroupBy(r => r.Kind).ToDictionary(g => g.Key, g => g.Count()),
                robotsDisallowed = rows.Count(r => r.RobotsStatus == "Disallowed"), entries, rows
            });
        }

        [HttpGet("entries")]
        public async Task<IActionResult> Entries([FromQuery] string? sourceKey, [FromQuery] string? youtubeId, [FromQuery] int take = 100, CancellationToken ct = default)
        {
            var q = _db.OfficialWebVideoEntries.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(sourceKey)) q = q.Where(e => e.SourceKey == sourceKey);
            if (!string.IsNullOrWhiteSpace(youtubeId)) q = q.Where(e => e.YouTubeVideoId == youtubeId);
            return Ok(await q.OrderByDescending(e => e.PublishedUtc).Take(Math.Clamp(take, 1, 1000)).ToListAsync(ct));
        }

        /// <summary>Akış taramasını bir tur çalıştırır (normal arka plan hattı).</summary>
        [HttpPost("crawl/run")]
        public async Task<IActionResult> Crawl([FromServices] OfficialWebFeedCrawler crawler, [FromQuery] int maxFeeds = 30, CancellationToken ct = default)
        {
            var (feeds, fresh) = await crawler.CrawlDueAsync(DateTime.UtcNow, Math.Clamp(maxFeeds, 1, 200), ct);
            return Ok(new { feeds, newEntries = fresh, httpCalls = crawler.HttpCalls });
        }

        /// <summary>Kalıcı yeniden doğrulamayı bir tur çalıştırır (normal arka plan hattı; satır silmez).</summary>
        [HttpPost("revalidate/run")]
        public async Task<IActionResult> Revalidate([FromServices] MatchVideoRevalidationService svc, [FromQuery] int max = 100, CancellationToken ct = default)
            => Ok(await svc.RunAsync(DateTime.UtcNow, Math.Clamp(max, 1, 1000), ct));
    }
}
