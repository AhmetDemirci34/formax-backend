using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.OfficialSources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// RESMÎ KAYNAK ALTYAPISI — kayıt defteri, istek defteri ve elle tur tetiği.
    /// Okuma uçları dış kaynağa ÇIKMAZ; yalnız kayıt defterini ve DB defterini okur.
    /// </summary>
    [ApiController]
    [Route("admin/official-sources")]
    public class AdminOfficialSourcesController : ControllerBase
    {
        private readonly FormaxDbContext _db;

        public AdminOfficialSourcesController(FormaxDbContext db) => _db = db;

        /// <summary>Kilitli 11 organizasyonun kaynakları ve ölçülmüş durumları (sıfır dış istek).</summary>
        [HttpGet("registry")]
        public IActionResult Registry()
        {
            var byLeague = OfficialSourceRegistry.LockedLeagueIds.Select(id => new
            {
                leagueId = id,
                sources = OfficialSourceRegistry.All.Where(s => s.LeagueIds.Contains(id)).OrderBy(s => s.Tier)
                    .Select(s => new { s.Key, s.Organization, tier = s.Tier.ToString(), s.Kind, s.Hosts, s.Capabilities, s.Status, s.EvidenceNote })
            });
            return Ok(new
            {
                allowedHosts = OfficialSourceRegistry.AllowedHosts.OrderBy(h => h),
                leagues = byLeague,
                other = OfficialSourceRegistry.All.Where(s => s.LeagueIds.Count == 0)
                    .Select(s => new { s.Key, s.Organization, tier = s.Tier.ToString(), s.Kind, s.Hosts, s.Capabilities, s.Status, s.EvidenceNote })
            });
        }

        /// <summary>
        /// İSTEK DEFTERİ — host/sağlayıcı/amaç bazında sayım + son satırlar (sıfır dış istek).
        /// </summary>
        [HttpGet("requests")]
        public async Task<IActionResult> Requests(
            [FromQuery] DateTime? sinceUtc, [FromQuery] string? purpose, [FromQuery] int take = 100,
            CancellationToken ct = default)
        {
            var since = sinceUtc ?? DateTime.UtcNow.AddHours(-24);
            var q = _db.OfficialSourceFetches.AsNoTracking().Where(x => x.RequestedAtUtc >= since);
            if (!string.IsNullOrWhiteSpace(purpose)) q = q.Where(x => x.Purpose == purpose);

            var summary = await q
                .GroupBy(x => new { x.Host, x.Provider, x.Purpose })
                .Select(g => new
                {
                    g.Key.Host, g.Key.Provider, g.Key.Purpose,
                    total = g.Count(),
                    network = g.Count(x => x.HttpStatus != null),
                    cacheHit = g.Count(x => x.CacheHit),
                    cacheMiss = g.Count(x => !x.CacheHit),
                    ok200 = g.Count(x => x.HttpStatus == 200),
                    notModified304 = g.Count(x => x.HttpStatus == 304),
                    failed = g.Count(x => x.Outcome != "Fetched" && x.Outcome != "NotModified" && x.Outcome != "RoundMemo"),
                    candidates = g.Sum(x => x.CandidateCount ?? 0),
                    accepted = g.Sum(x => x.AcceptedCount ?? 0)
                })
                .OrderBy(x => x.Host).ThenBy(x => x.Purpose)
                .ToListAsync(ct);

            var rows = await q.OrderByDescending(x => x.Id).Take(Math.Clamp(take, 1, 1000))
                .Select(x => new
                {
                    x.Id, x.RequestedAtUtc, x.SourceKey, x.Provider, x.Host, x.Purpose, x.RoundKey, x.MatchId,
                    x.HttpStatus, x.Outcome, x.CacheHit, x.ContentChanged, x.Bytes, x.DurationMs,
                    x.CandidateCount, x.AcceptedCount, x.Decision, x.Url
                })
                .ToListAsync(ct);

            return Ok(new { sinceUtc = since, summary, rows });
        }

        /// <summary>Maç merkezi turunu şimdi çalıştırır (kritik gelişme + resmî saat).</summary>
        [HttpPost("match-centre/round")]
        public async Task<IActionResult> MatchCentreRound([FromServices] Formax.Infrastructure.BackgroundJobs.OfficialMatchCentreJob job, CancellationToken ct)
            => Ok(await job.RunOnceAsync(ct));

        /// <summary>Doğrulanmış kritik gelişmeler (sıfır dış istek).</summary>
        [HttpGet("critical")]
        public async Task<IActionResult> Critical([FromQuery] int? matchId, [FromQuery] int take = 100, CancellationToken ct = default)
        {
            var q = _db.MatchCriticalDevelopments.AsNoTracking();
            if (matchId.HasValue) q = q.Where(d => d.MatchId == matchId.Value);
            return Ok(await q.OrderByDescending(d => d.Id).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct));
        }

        /// <summary>Kadro turunu şimdi çalıştırır (aynı slot kuralı; takvim dışı istek üretmez).</summary>
        [HttpPost("lineups/round")]
        public async Task<IActionResult> LineupRound([FromServices] OfficialLineupCollector collector, CancellationToken ct)
            => Ok(await collector.RunRoundAsync(DateTime.UtcNow, ct));
    }
}
