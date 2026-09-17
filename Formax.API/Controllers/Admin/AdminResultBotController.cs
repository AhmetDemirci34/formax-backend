using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.OfficialSources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// API'SİZ RESMÎ SONUÇ + İSTATİSTİK BOTU — teşhis. GET uçları salt DB okur. POST uçları normal arka plan hattının bir
    /// turunu çalıştırır (aynı plan, aynı kilit, aynı uzlaşma); elle sonuç ya da istatistik yazan uç YOKTUR.
    /// </summary>
    [ApiController]
    [Route("admin/results")]
    public sealed class AdminResultBotController : ControllerBase
    {
        private readonly FormaxDbContext _db;
        public AdminResultBotController(FormaxDbContext db) => _db = db;

        /// <summary>
        /// KAPSAM VE SAĞLIK RAPORU — organizasyon başına resmî sonuç kaynağı: ana kaynak, yedek kaynak, kapsam durumu,
        /// son başarılı okuma, son doğrulanmış maç, son hata sınıfı, sonraki deneme, botun dış istek sayısı, gerçek sonuç
        /// yazım sayısı, çelişki sayısı ve şema sağlığı.
        ///
        /// Bu uç YALNIZ DB/yerel sağlık kayıtlarını okur; sayfa açıldığında dış kaynağa İSTEK ÇIKMAZ (ne indirici ne de
        /// bot bu denetleyicide çözülür).
        /// </summary>
        [HttpGet("organizations")]
        public async Task<IActionResult> Organizations(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var since = now.AddDays(-30);
            var sources = await _db.OfficialDataSources.AsNoTracking().ToListAsync(ct);
            var covered = await _db.Matches.AsNoTracking()
                .Where(m => m.ResultSource != null && m.ResultSource.StartsWith("official:") && m.ResultUpdatedAtUtc >= since)
                .GroupBy(m => m.ResultSource!).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
            var stats = await _db.MatchTeamStatistics.AsNoTracking()
                .Where(s => s.Source != null && s.Source.StartsWith("official:"))
                .GroupBy(s => s.Source!).Select(g => new { g.Key, Matches = g.Select(x => x.MatchId).Distinct().Count() }).ToListAsync(ct);
            var names = await _db.Matches.AsNoTracking().Where(m => LockedCompetitions.All.Contains(m.LeagueId))
                .GroupBy(m => m.LeagueId).Select(g => new { LeagueId = g.Key, Name = g.Max(x => x.League) }).ToListAsync(ct);
            // Botun dış istekleri (defter) — kaynak başına son 30 gün; RoundMemo/304 ağa çıkmayan okumadır.
            var fetches = await _db.OfficialSourceFetches.AsNoTracking()
                .Where(f => f.RequestedAtUtc >= since && f.Purpose == OfficialPurposes.Result)
                .GroupBy(f => f.SourceKey)
                .Select(g => new
                {
                    SourceKey = g.Key,
                    Requests = g.Count(),
                    Network = g.Count(x => !x.CacheHit && x.Outcome != OfficialFetchOutcomes.RoundMemo),
                    Failures = g.Count(x => x.Outcome != OfficialFetchOutcomes.Fetched && x.Outcome != OfficialFetchOutcomes.NotModified
                                            && x.Outcome != OfficialFetchOutcomes.RoundMemo),
                    LastUtc = g.Max(x => x.RequestedAtUtc)
                }).ToListAsync(ct);
            var conflicts = await _db.MatchResultObservations.AsNoTracking()
                .Where(o => o.ConflictStatus == "Conflict" || o.Decision == "ConflictRecorded")
                .GroupBy(o => o.SourceKey).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
            // Plan satırları — organizasyon başına sonraki deneme, bekleyen maç, son hata sınıfı.
            var plans = await _db.MatchResultChecks.AsNoTracking()
                .Where(c => LockedCompetitions.All.Contains(c.LeagueId))
                .GroupBy(c => c.LeagueId)
                .Select(g => new
                {
                    LeagueId = g.Key,
                    Pending = g.Count(x => x.State == "Pending"),
                    NoSource = g.Count(x => x.State == "NoOfficialSource"),
                    Resolved = g.Count(x => x.State == "Resolved"),
                    NextAttemptUtc = g.Where(x => x.State == "Pending" || x.State == "Postponed").Min(x => (DateTime?)x.NextCheckUtc),
                    LastCheckUtc = g.Max(x => x.LastCheckUtc)
                }).ToListAsync(ct);
            var lastErrors = await _db.MatchResultChecks.AsNoTracking()
                .Where(c => LockedCompetitions.All.Contains(c.LeagueId) && c.LastErrorClass != null)
                .OrderByDescending(c => c.LastCheckUtc)
                .Select(c => new { c.LeagueId, c.LastErrorClass, c.LastValidationStatus, c.LastCheckUtc })
                .Take(400).ToListAsync(ct);
            // Son doğrulanmış maç (organizasyon başına) — kanıt satırı.
            var lastVerified = await _db.Matches.AsNoTracking()
                .Where(m => LockedCompetitions.All.Contains(m.LeagueId) && m.ResultSource != null
                            && m.ResultSource.StartsWith("official:") && m.ResultUpdatedAtUtc != null)
                .OrderByDescending(m => m.ResultUpdatedAtUtc)
                .Select(m => new { m.LeagueId, m.Id, Home = m.HomeTeam!.Name, Away = m.AwayTeam!.Name, m.HomeScore, m.AwayScore,
                    m.MatchDate, m.ResultUpdatedAtUtc, m.ResultSource, m.ResultVerificationStatus })
                .Take(600).ToListAsync(ct);

            var rows = LockedCompetitions.All.Select(leagueId =>
            {
                var forLeague = OfficialSourceRegistry.All
                    .Where(d => d.LeagueIds.Contains(leagueId) && d.Capabilities.Contains(OfficialPurposes.Result))
                    .OrderBy(d => d.Status == OfficialSourceStatuses.Verified ? 0 : 1).ThenBy(d => d.Tier).ToList();
                var result = forLeague.FirstOrDefault()
                             ?? OfficialSourceRegistry.All.Where(d => d.LeagueIds.Contains(leagueId)
                                     && d.Tier is OfficialSourceTier.Federation or OfficialSourceTier.LeagueMatchCentre)
                                 .OrderBy(d => d.Status == OfficialSourceStatuses.Verified ? 0 : 1).FirstOrDefault();
                var fallback = forLeague.Where(d => d.Status == OfficialSourceStatuses.Verified && d.Key != result?.Key)
                    .Select(d => d.Key).FirstOrDefault();
                var row = result == null ? null : sources.FirstOrDefault(s => s.SourceId == result.Key);
                var tag = result == null ? null : OfficialLineupCollector.ProviderPrefix + result.Key;
                var fetch = result == null ? null : fetches.FirstOrDefault(f => f.SourceKey == result.Key);
                var plan = plans.FirstOrDefault(p => p.LeagueId == leagueId);
                var verified = lastVerified.FirstOrDefault(v => v.LeagueId == leagueId);
                var available = result?.Status == OfficialSourceStatuses.Verified;
                return new
                {
                    leagueId,
                    organization = names.FirstOrDefault(n => n.LeagueId == leagueId)?.Name,
                    resultSource = result?.Key,
                    fallbackSource = fallback,
                    sourceName = result?.Organization,
                    sourceType = result?.Tier.ToString(),
                    contentKind = result?.Kind,
                    registryStatus = result?.Status ?? "NotConfigured",
                    // Kapsam: Supported = doğrulanmış ana kaynak + yedek; Partial = yalnız ana kaynak;
                    // Blocked = kaynak var ama erişilemiyor/yasak; Missing = hiç kaynak yok.
                    coverage = !available
                        ? result?.Status is OfficialSourceStatuses.Blocked or OfficialSourceStatuses.Unsupported ? "Blocked" : "Missing"
                        : fallback == null ? "Partial" : "Supported",
                    // Teşhis: doğrulanmış resmî sonuç kaynağı yoksa bu organizasyonun maçları "ResultSourceUnavailable".
                    resultSourceStatus = available ? "Available" : "ResultSourceUnavailable",
                    capabilities = result?.Capabilities,
                    robotsStatus = row?.RobotsStatus ?? "Unknown",
                    parser = row?.ParserVersion,
                    // Şema sağlığı: son okuma ayrıştırılabildi mi (ParseFailed son hata olarak duruyorsa şema kaymış olabilir).
                    schemaHealth = row == null ? "Unknown"
                        : row.LastError != null && row.LastError.StartsWith(OfficialReadOutcomes.ParseFailed, StringComparison.Ordinal) ? "SchemaDrift"
                        : row.LastSuccessUtc != null ? "Ok" : "Unknown",
                    healthy = row != null && row.IsEnabled && row.ConsecutiveFailureCount == 0 && row.LastSuccessUtc != null,
                    lastCheckedUtc = row?.LastCheckedUtc,
                    lastSuccessUtc = row?.LastSuccessUtc,
                    consecutiveFailures = row?.ConsecutiveFailureCount,
                    circuitBreakerUntilUtc = row?.CircuitBreakerUntilUtc,
                    lastError = row?.LastError,
                    lastErrorClass = lastErrors.FirstOrDefault(e => e.LeagueId == leagueId)?.LastErrorClass,
                    lastValidationStatus = lastErrors.FirstOrDefault(e => e.LeagueId == leagueId)?.LastValidationStatus,
                    nextAttemptUtc = plan?.NextAttemptUtc,
                    pendingChecks = plan?.Pending ?? 0,
                    noSourceChecks = plan?.NoSource ?? 0,
                    resolvedChecks = plan?.Resolved ?? 0,
                    botExternalRequestsLast30Days = fetch?.Requests ?? 0,
                    botNetworkRequestsLast30Days = fetch?.Network ?? 0,
                    botFailedRequestsLast30Days = fetch?.Failures ?? 0,
                    lastExternalRequestUtc = fetch?.LastUtc,
                    resultsWrittenLast30Days = tag == null ? 0 : covered.FirstOrDefault(c => c.Key == tag)?.Count ?? 0,
                    conflicts = result == null ? 0 : conflicts.FirstOrDefault(c => c.Key == result.Key)?.Count ?? 0,
                    matchesWithOfficialStatistics = tag == null ? 0 : stats.FirstOrDefault(c => c.Key == tag)?.Matches ?? 0,
                    lastVerifiedMatch = verified == null ? null : new
                    {
                        verified.Id, verified.Home, verified.Away,
                        score = verified.HomeScore + "-" + verified.AwayScore,
                        kickoffUtc = verified.MatchDate, verified.ResultUpdatedAtUtc, verified.ResultSource, verified.ResultVerificationStatus
                    },
                    evidence = result?.EvidenceNote
                };
            }).ToList();
            return Ok(new
            {
                generatedAtUtc = now,
                summary = new
                {
                    organizations = rows.Count,
                    supported = rows.Count(r => r.coverage == "Supported"),
                    partial = rows.Count(r => r.coverage == "Partial"),
                    blocked = rows.Count(r => r.coverage == "Blocked"),
                    missing = rows.Count(r => r.coverage == "Missing"),
                    withVerifiedPrimarySource = rows.Count(r => r.resultSourceStatus == "Available")
                },
                organizations = rows,
                catalog = sources
            });
        }

        /// <summary>Türkiye günü (yyyy-MM-dd) için sonuç kontrol planı + kanonik durum + gözlem gecikmesi.</summary>
        [HttpGet("day")]
        public async Task<IActionResult> Day([FromQuery] string? date, CancellationToken ct)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
            var day = DateTime.TryParse(date, out var d) ? d.Date : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(day, tz);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1), tz);
            var rows = await (from m in _db.Matches.AsNoTracking()
                              where m.MatchDate >= startUtc && m.MatchDate < endUtc && LockedCompetitions.All.Contains(m.LeagueId)
                              join c in _db.MatchResultChecks.AsNoTracking() on m.Id equals c.MatchId into cj
                              from c in cj.DefaultIfEmpty()
                              join s in _db.MatchStatisticsChecks.AsNoTracking() on m.Id equals s.MatchId into sj
                              from s in sj.DefaultIfEmpty()
                              orderby m.MatchDate
                              select new
                              {
                                  m.Id, m.LeagueId, Home = m.HomeTeam!.Name, Away = m.AwayTeam!.Name, m.MatchDate, m.Status, m.HomeScore, m.AwayScore,
                                  m.HalfTimeHomeScore, m.HalfTimeAwayScore, m.ResultDetail, m.ResultSource, m.ResultUpdatedAtUtc, m.ResultVerificationStatus,
                                  Check = c == null ? null : new { c.State, c.AttemptCount, c.NextCheckUtc, c.LastCheckUtc, c.LastOutcome, c.LastSourceKey, c.LastErrorClass, c.LastValidationStatus, c.FirstFinalSeenUtc, c.ResolvedAtUtc, c.ResolvedStatus, c.LastNotFinalCheckUtc, c.SourcePublishedFinalAtUtc },
                                  Statistics = s == null ? null : new { s.State, s.Completeness, s.AttemptCount, s.NextCheckUtc, s.LastOutcome, s.LastSourceKey }
                              }).ToListAsync(ct);
            return Ok(new
            {
                dayIstanbul = day.ToString("yyyy-MM-dd"), startUtc, endUtc,
                matches = rows.Select(r => new
                {
                    r.Id, r.LeagueId, r.Home, r.Away, kickoffUtc = r.MatchDate, r.Status, score = r.Status == MatchStatuses.Finished ? $"{r.HomeScore}-{r.AwayScore}" : null,
                    halfTime = r.HalfTimeHomeScore.HasValue ? $"{r.HalfTimeHomeScore}-{r.HalfTimeAwayScore}" : null,
                    r.ResultDetail, r.ResultSource, r.ResultUpdatedAtUtc, r.ResultVerificationStatus, r.Check, r.Statistics,
                    // Gecikme: kaynağın maçı ilk kez "bitti" gösterdiği gözlem ile kanonik yazım arası (dk).
                    writeDelayMinutes = r.Check?.FirstFinalSeenUtc != null && r.Check.ResolvedAtUtc != null
                        ? Math.Round((r.Check.ResolvedAtUtc.Value - r.Check.FirstFinalSeenUtc.Value).TotalMinutes, 1) : (double?)null,
                    minutesFromKickoffToWrite = r.ResultUpdatedAtUtc != null ? Math.Round((r.ResultUpdatedAtUtc.Value - r.MatchDate).TotalMinutes, 1) : (double?)null,
                    // YAYIN → YAZIM GECİKMESİ: kaynak yayın anını veriyorsa (UEFA fullTimeAt) kesin değer; vermiyorsa üst sınır =
                    // yazım − kaynağın en son "henüz final değil" dediği kontrol (yayın bu andan SONRA oldu).
                    publishToWriteMinutes = r.Check?.SourcePublishedFinalAtUtc != null && r.ResultUpdatedAtUtc != null
                        ? Math.Round((r.ResultUpdatedAtUtc.Value - r.Check.SourcePublishedFinalAtUtc.Value).TotalMinutes, 1) : (double?)null,
                    publishToWriteUpperBoundMinutes = r.Check?.LastNotFinalCheckUtc != null && r.ResultUpdatedAtUtc != null && r.ResultUpdatedAtUtc > r.Check.LastNotFinalCheckUtc
                        ? Math.Round((r.ResultUpdatedAtUtc.Value - r.Check.LastNotFinalCheckUtc.Value).TotalMinutes, 1) : (double?)null,
                    resultSourceStatus = OfficialSourceRegistry.VerifiedFor(r.LeagueId, OfficialPurposes.Result).Count > 0 ? "Available" : "ResultSourceUnavailable"
                })
            });
        }

        [HttpGet("observations/{matchId:int}")]
        public async Task<IActionResult> Observations(int matchId, CancellationToken ct)
            => Ok(new
            {
                results = await _db.MatchResultObservations.AsNoTracking().Where(o => o.MatchId == matchId).OrderBy(o => o.ObservedAtUtc).ToListAsync(ct),
                statistics = await _db.MatchStatisticObservations.AsNoTracking().Where(o => o.MatchId == matchId).OrderBy(o => o.ObservedAtUtc).ToListAsync(ct),
                canonicalStatistics = await _db.MatchTeamStatistics.AsNoTracking().Where(s => s.MatchId == matchId).ToListAsync(ct),
                check = await _db.MatchResultChecks.AsNoTracking().FirstOrDefaultAsync(c => c.MatchId == matchId, ct),
                statisticsCheck = await _db.MatchStatisticsChecks.AsNoTracking().FirstOrDefaultAsync(c => c.MatchId == matchId, ct)
            });

        /// <summary>İstatistik tamlık özeti (tam / kısmi / hiç) — plan satırlarından.</summary>
        [HttpGet("statistics-summary")]
        public async Task<IActionResult> StatisticsSummary(CancellationToken ct)
            => Ok(await _db.MatchStatisticsChecks.AsNoTracking().GroupBy(s => new { s.State, s.Completeness })
                .Select(g => new { g.Key.State, g.Key.Completeness, Count = g.Count() }).ToListAsync(ct));

        [HttpGet("conflicts")]
        public async Task<IActionResult> Conflicts(CancellationToken ct)
            => Ok(new
            {
                results = await _db.MatchResultObservations.AsNoTracking().Where(o => o.ConflictStatus == "Conflict" || o.Decision == "ConflictRecorded" || o.Decision == "ConflictResolvedAfterConfirmation")
                    .OrderByDescending(o => o.ObservedAtUtc).Take(200).ToListAsync(ct),
                statistics = await _db.MatchStatisticObservations.AsNoTracking().Where(o => o.Decision == "ConflictRecorded")
                    .OrderByDescending(o => o.ObservedAtUtc).Take(200).ToListAsync(ct)
            });

        /// <summary>
        /// Belirli maçları normal sonuç botu hattından HEMEN geçirir (kontrol zamanını öne çeker). Önceki/sonraki kanonik durum ve süre döner.
        /// </summary>
        [HttpPost("recheck")]
        public async Task<IActionResult> Recheck([FromServices] OfficialResultBotService bot, [FromQuery] string ids, CancellationToken ct)
        {
            var list = (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var v) ? v : 0).Where(v => v > 0).Distinct().Take(50).ToList();
            object State(int id) => _db.Matches.AsNoTracking().Where(m => m.Id == id)
                .Select(m => new { m.Id, m.Status, m.HomeScore, m.AwayScore, m.HalfTimeHomeScore, m.HalfTimeAwayScore, m.ResultSource, m.ResultDetail, m.ResultUpdatedAtUtc, m.ResultVerificationStatus })
                .FirstOrDefault()!;
            var before = list.Select(State).ToList();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var report = await bot.RecheckNowAsync(list, DateTime.UtcNow, ct);
            sw.Stop();
            var after = list.Select(State).ToList();
            return Ok(new { elapsedMs = sw.ElapsedMilliseconds, before, report, after });
        }

        /// <summary>Sonuç botunun bir turu (normal hat).</summary>
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromServices] OfficialResultBotService bot, CancellationToken ct)
            => Ok(await bot.RunCycleAsync(DateTime.UtcNow, ct));

        /// <summary>İstatistik botunun bir turu (normal hat).</summary>
        [HttpPost("statistics/run")]
        public async Task<IActionResult> RunStatistics([FromServices] OfficialStatisticsBotService bot, CancellationToken ct)
            => Ok(await bot.RunCycleAsync(DateTime.UtcNow, ct));
    }
}
