using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>
    /// BİTMİŞ MAÇ ANALİZ METNİNİ YAZAN TEK YER — <see cref="BackgroundJobs.PostMatchEnrichmentJob"/>
    /// turunun bir aşaması.
    ///
    /// Girdi yalnız FORMAX DB'sidir (Matches + MatchEventRecords + MatchTeamStatistics): dış
    /// kaynak, API-Football ve LLM çağrısı YOKTUR. Girdi özeti değişmedikçe satır yeniden
    /// yazılmaz; olaylar sonradan gelirse (ör. resmî olaylar bir sonraki turda) metin yenilenir.
    /// </summary>
    public sealed class PostMatchSummaryService
    {
        private readonly FormaxDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<PostMatchSummaryService> _log;

        public PostMatchSummaryService(FormaxDbContext db, IConfiguration config, ILogger<PostMatchSummaryService> log)
        {
            _db = db; _config = config; _log = log;
        }

        /// <returns>Yazılan (yeni ya da güncellenen) satır sayısı.</returns>
        public const string InsufficientGenerator = "InsufficientData";

        public async Task<int> RunCycleAsync(DateTime utcNow, CancellationToken ct = default)
        {
            var since = utcNow.AddHours(-Math.Max(24, _config.GetValue("PostMatch:Summary:LookbackHours", 96)));
            var locked = LockedCompetitions.All.ToList();

            var matches = await _db.Matches.AsNoTracking()
                .Where(m => m.Status == MatchStatuses.Finished && m.MatchDate >= since && m.MatchDate <= utcNow
                            && locked.Contains(m.LeagueId))
                .Select(m => new { m.Id, m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore, m.HalfTimeHomeScore, m.HalfTimeAwayScore })
                .ToListAsync(ct).ConfigureAwait(false);

            // ESKİ MAÇ BACKFILL (sayfalı): analizi hiç yazılmamış eski bitmiş maçlar tur başına bir sayfa.
            // Eski maçın detay ekranı boş kalmasın; bütün geçmiş tek seferde belleğe alınmaz.
            var page = Math.Max(0, _config.GetValue("PostMatch:Summary:BackfillBatch", 100));
            if (page > 0)
            {
                // Ufuk sınırı yok (15.09.2026): yetersiz veri durumu da satır olarak yazıldığı için sayfa ilerler, takılmaz.
                var older = await _db.Matches.AsNoTracking()
                    .Where(m => m.Status == MatchStatuses.Finished && m.MatchDate < since
                                && locked.Contains(m.LeagueId)
                                && !_db.MatchPostMatchSummaries.Any(s => s.MatchId == m.Id))
                    .OrderByDescending(m => m.MatchDate).Take(page)
                    .Select(m => new { m.Id, m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore, m.HalfTimeHomeScore, m.HalfTimeAwayScore })
                    .ToListAsync(ct).ConfigureAwait(false);
                matches.AddRange(older);
            }

            var written = 0;
            foreach (var m in matches)
            {
                ct.ThrowIfCancellationRequested();
                var result = await ComposeAsync(m.Id, ct).ConfigureAwait(false);
                if (result == null) continue;

                var row = await _db.MatchPostMatchSummaries.FirstOrDefaultAsync(s => s.MatchId == m.Id, ct).ConfigureAwait(false);
                if (row != null && row.InputHash == result.InputHash) continue;
                if (row == null) { row = new MatchPostMatchSummary { MatchId = m.Id }; _db.MatchPostMatchSummaries.Add(row); }
                row.InputHash = result.InputHash;
                // YETERSİZ VERİ: yalnız sonuç cümlesi kurulabildiyse (olay/istatistik yok) bu skorun başka kelimelerle tekrarıdır —
                // analiz diye yazılmaz; ekran dürüst "yeterli doğrulanmış veri yok" durumunu gösterir.
                var insufficient = result.Sentences.Count < 2;
                row.Text = insufficient ? string.Empty : result.Text.Length <= 1200 ? result.Text : result.Text[..1200];
                row.EvidenceJson = result.EvidenceJson;
                row.Generator = insufficient ? InsufficientGenerator : "Deterministic";
                row.LlmCalls = 0;
                row.GeneratedAtUtc = utcNow;
                written++;
            }

            if (written > 0)
            {
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                _log.LogInformation("[POST-MATCH SUMMARY] {Count} maç için analiz metni yazıldı (llm=0).", written);
            }
            return written;
        }

        /// <summary>Tek maçın metni — salt DB okuması; maç bitmemişse null.</summary>
        public async Task<PostMatchSummaryResult?> ComposeAsync(int matchId, CancellationToken ct = default)
        {
            var m = await _db.Matches.AsNoTracking()
                .Where(x => x.Id == matchId && x.Status == MatchStatuses.Finished)
                .Select(x => new { x.HomeTeamId, x.AwayTeamId, x.HomeScore, x.AwayScore, x.HalfTimeHomeScore, x.HalfTimeAwayScore })
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (m == null) return null;

            var names = await _db.Teams.AsNoTracking()
                .Where(t => t.Id == m.HomeTeamId || t.Id == m.AwayTeamId)
                .Select(t => new { t.Id, t.Name }).ToListAsync(ct).ConfigureAwait(false);
            var home = names.FirstOrDefault(t => t.Id == m.HomeTeamId)?.Name;
            var away = names.FirstOrDefault(t => t.Id == m.AwayTeamId)?.Name;
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) return null;

            var records = await _db.MatchEventRecords.AsNoTracking()
                .Where(e => e.MatchId == matchId)
                .OrderBy(e => e.Minute).ThenBy(e => e.ExtraMinute).ThenBy(e => e.Id)
                .ToListAsync(ct).ConfigureAwait(false);
            var stats = await _db.MatchTeamStatistics.AsNoTracking()
                .Where(s => s.MatchId == matchId).ToListAsync(ct).ConfigureAwait(false);

            return PostMatchSummaryComposer.Compose(new PostMatchSummaryInput(
                home!, away!, m.HomeScore, m.AwayScore, m.HalfTimeHomeScore, m.HalfTimeAwayScore,
                PostMatchSummaryComposer.FromRecords(records, home!, away!),
                PostMatchSummaryComposer.StatsFrom(
                    stats.FirstOrDefault(s => s.Side == "Home"), stats.FirstOrDefault(s => s.Side == "Away"))));
        }
    }
}
