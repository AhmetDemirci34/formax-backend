using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Lineups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// GEÇMİŞ KADRO DOLDURMA — kontrollü tetik ve durum.
    ///
    /// Doldurma uygulama açılışında KENDİLİĞİNDEN çalışmaz; yalnız buradan başlatılır ve
    /// checkpoint'ten devam eder. API-Football'a hiçbir istek atmaz.
    /// </summary>
    [ApiController]
    [Route("admin/lineup-backfill")]
    public sealed class AdminLineupBackfillController : ControllerBase
    {
        private readonly LineupBackfillService _backfill;
        private readonly FormaxDbContext _db;

        public AdminLineupBackfillController(LineupBackfillService backfill, FormaxDbContext db)
        {
            _backfill = backfill; _db = db;
        }

        /// <summary>İlerleme: kaynak × sezon checkpoint'leri, deneme dağılımı ve kadro kapsamı.</summary>
        [HttpGet("status")]
        public async Task<IActionResult> Status(CancellationToken ct)
        {
            var checkpoints = await _db.LineupBackfillCheckpoints.AsNoTracking()
                .OrderBy(c => c.LeagueId).ThenByDescending(c => c.SeasonLabel).ToListAsync(ct);
            var attempts = await _db.LineupBackfillAttempts.AsNoTracking()
                .GroupBy(a => new { a.LeagueId, a.Outcome })
                .Select(g => new { g.Key.LeagueId, g.Key.Outcome, Count = g.Count() })
                .ToListAsync(ct);
            var coverage = await _db.MatchLineups.AsNoTracking()
                .Where(l => l.Provider != null && l.Provider.StartsWith("official:") && l.VerificationStatus == "Verified")
                .Join(_db.Matches.AsNoTracking(), l => l.MatchId, m => m.Id, (l, m) => new { m.LeagueId, l.DataQuality, l.BackfilledAtUtc })
                .GroupBy(x => x.LeagueId)
                .Select(g => new
                {
                    leagueId = g.Key,
                    verified = g.Count(),
                    backfilled = g.Count(x => x.BackfilledAtUtc != null),
                    withMinutes = g.Count(x => x.DataQuality == "WithMinutes")
                })
                .ToListAsync(ct);

            return Ok(new
            {
                running = LineupBackfillService.IsRunning,
                supportedLeagues = _backfill.SupportedLeagues().Select(x => new { x.LeagueId, source = x.Source.SourceKey }),
                checkpoints = checkpoints.Select(c => new
                {
                    c.SourceKey, c.LeagueId, c.SeasonLabel, c.Status, c.SourceMatches,
                    c.ProcessedMatches, c.VerifiedMatches, c.FailedMatches, c.LastRunAtUtc, c.CompletedAtUtc, c.LastError
                }),
                attempts = attempts.GroupBy(a => a.LeagueId).Select(g => new
                {
                    leagueId = g.Key,
                    outcomes = g.ToDictionary(x => x.Outcome, x => x.Count)
                }),
                coverage
            });
        }

        /// <summary>
        /// DRY-RUN — hiçbir şey yazmadan okuma, eşleme ve doğrulamayı ölçer. Büyük doldurmadan
        /// ÖNCE her lig için çalıştırılmalıdır.
        /// </summary>
        [HttpPost("dry-run")]
        public Task<IActionResult> DryRun(
            [FromQuery] int? leagueId, [FromQuery] string? seasonId, [FromQuery] int maxMatches = 5,
            [FromQuery] bool includeMinutes = true, CancellationToken ct = default)
            => RunAsync(leagueId, seasonId, maxMatches, dryRun: true, includeMinutes, ct);

        /// <summary>GERÇEK YAZIM — tavanla sınırlı, checkpoint'ten devam eden koşu.</summary>
        [HttpPost("run")]
        public Task<IActionResult> Run(
            [FromQuery] int? leagueId, [FromQuery] string? seasonId, [FromQuery] int maxMatches = 25,
            [FromQuery] bool includeMinutes = true, CancellationToken ct = default)
            => RunAsync(leagueId, seasonId, maxMatches, dryRun: false, includeMinutes, ct);

        /// <summary>Çalışan koşuyu durdurur; checkpoint korunur ve sonraki koşu kaldığı yerden devam eder.</summary>
        [HttpPost("stop")]
        public IActionResult Stop()
        {
            LineupBackfillService.RequestStop();
            return Ok(new { stopped = true, running = LineupBackfillService.IsRunning });
        }

        private async Task<IActionResult> RunAsync(
            int? leagueId, string? seasonId, int maxMatches, bool dryRun, bool includeMinutes, CancellationToken ct)
        {
            if (maxMatches is < 1 or > 2000)
                return BadRequest(new { error = "maxMatches 1..2000 aralığında olmalı (zorunlu tavan)." });

            var report = await _backfill.RunAsync(new LineupBackfillRequest(
                LeagueIds: leagueId.HasValue ? new[] { leagueId.Value } : null,
                SeasonIds: string.IsNullOrWhiteSpace(seasonId) ? null : new[] { seasonId },
                MaxMatches: maxMatches,
                DryRun: dryRun,
                IncludeMinutes: includeMinutes), ct);

            return Ok(new
            {
                report.DryRun, report.StartedAtUtc, report.DurationSeconds, report.MaxMatches, report.StoppedAtLimit,
                report.Seasons, report.Processed, report.Verified, report.Unchanged, report.Failed, report.SourceRequests,
                requestsPerMatch = report.Processed == 0 ? 0 : Math.Round(report.SourceRequests / (double)report.Processed, 2),
                report.Notes,
                seasons = report.Seasons_
            });
        }
    }
}
