using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.PostMatch;
using Formax.Application.Interfaces;
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
    /// BİTMİŞ MAÇ OLAY + İSTATİSTİK TOPLAMA — kanonik yazıcı.
    ///
    /// AYRI BİR JOB YIĞINI KURULMADI (ürün kararı 06.09.2026): bu servis, zaten var
    /// olan <c>PostMatchEnrichmentJob</c> turunun bir aşaması olarak çağrılır. Yeni
    /// bir tekrar eden iş, kendi başlangıç gecikmesi ve kendi kotasıyla üçüncü bir
    /// zamanlama kaynağı yaratırdı.
    ///
    /// KISMİ BAŞARI KORUNUR: olay ve istatistik AYRI uçlardır, AYRI bütçeleri vardır
    /// ve AYRI yazılırlar. Olay başarılı olup istatistik başarısız olduğunda olaylar
    /// kaydedilir; istatistik bir sonraki gün yeniden denenir. Tek bir "maç detayı"
    /// çağrısı olsaydı, birinin hatası diğerinin verisini de çöpe atardı.
    ///
    /// BOŞ CEVAP ≠ GERÇEK SIFIR: sağlayıcı hatası (Succeeded=false) ile "bu maçta
    /// istatistik yok" birbirinden ayrıdır. Hata durumunda HİÇBİR ŞEY YAZILMAZ —
    /// aksi hâlde boş satır "veri var" sayılır ve bir daha hiç denenmezdi.
    /// </summary>
    public sealed class PostMatchDataIngestionService
    {
        private readonly FormaxDbContext _db;
        private readonly ISportsDataProvider _provider;
        private readonly IFixtureSyncRepository _ledger;
        private readonly IConfiguration _config;
        private readonly ILogger<PostMatchDataIngestionService> _logger;

        public PostMatchDataIngestionService(
            FormaxDbContext db,
            ISportsDataProvider provider,
            IFixtureSyncRepository ledger,
            IConfiguration config,
            ILogger<PostMatchDataIngestionService> logger)
        {
            _db = db;
            _provider = provider;
            _ledger = ledger;
            _config = config;
            _logger = logger;
        }

        /// <summary>Bir turda kaç maça bakılacağı (bütçe zaten ayrıca sınırlar).</summary>
        private int MaxMatchesPerCycle =>
            Math.Max(0, _config.GetValue("PostMatch:Data:MaxMatchesPerCycle", 10));

        private int EventsDailyCap =>
            Math.Max(0, _config.GetValue("PostMatch:Data:MaxEventRequestsPerUtcDay",
                PostMatchDataPolicy.DefaultEventsPerUtcDay));

        private int StatisticsDailyCap =>
            Math.Max(0, _config.GetValue("PostMatch:Data:MaxStatisticsRequestsPerUtcDay",
                PostMatchDataPolicy.DefaultStatisticsPerUtcDay));

        private int LookbackHours =>
            Math.Max(24, _config.GetValue("PostMatch:Data:LookbackHours", 96));

        /// <summary>Bir turun sonucu — teşhis ve test için.</summary>
        public sealed record CycleResult(
            int Candidates, int EventRequests, int StatisticsRequests,
            int EventRowsWritten, int StatisticsRowsWritten);

        /// <summary>
        /// BİR TUR. Arka plan işinden çağrılır; okuma yolundan ASLA çağrılmaz.
        /// </summary>
        public async Task<CycleResult> RunCycleAsync(CancellationToken ct = default)
        {
            if (!_config.GetValue("PostMatch:Data:Enabled", true))
                return new CycleResult(0, 0, 0, 0, 0);

            var nowUtc = DateTime.UtcNow;
            var candidates = await BuildCandidatesAsync(nowUtc, ct).ConfigureAwait(false);
            if (candidates.Count == 0) return new CycleResult(0, 0, 0, 0, 0);

            int eventReqs = 0, statReqs = 0, eventRows = 0, statRows = 0;
            var processed = 0;

            foreach (var c in candidates)
            {
                ct.ThrowIfCancellationRequested();
                if (processed >= MaxMatchesPerCycle) break;
                processed++;

                // ── OLAYLAR ────────────────────────────────────────────────────
                if (!c.HasEvents &&
                    _ledger.TryReserveFixtureAttempt(
                        c.ExternalFixtureId, FixtureRefreshPurposes.PostMatchEvents,
                        PostMatchDataPolicy.PerFixtureCooldown, EventsDailyCap, nowUtc))
                {
                    eventReqs++;
                    var outcome = "NoData";
                    try
                    {
                        var result = await _provider
                            .GetFinishedMatchEventsAsync(c.ExternalFixtureId, ct)
                            .ConfigureAwait(false);

                        if (result.Succeeded)
                        {
                            var written = await WriteEventsAsync(c, result, nowUtc, ct)
                                .ConfigureAwait(false);
                            eventRows += written;
                            outcome = written > 0 ? "Applied" : "NoData";
                        }
                        else
                        {
                            // SAĞLAYICI HATASI — hiçbir şey yazılmaz, yarın yeniden denenir.
                            outcome = "ProviderError";
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        outcome = "ProviderError";
                        _logger.LogWarning(ex,
                            "[POST-MATCH DATA] events yazimi basarisiz — match {MatchId}", c.MatchId);
                    }

                    _ledger.RecordFixtureAttemptOutcome(
                        c.ExternalFixtureId, FixtureRefreshPurposes.PostMatchEvents, nowUtc, outcome);
                }

                // ── İSTATİSTİKLER ──────────────────────────────────────────────
                // Olay adımının sonucu buraya ETKİ ETMEZ: kısmi başarı korunur.
                if (!c.HasStatistics &&
                    _ledger.TryReserveFixtureAttempt(
                        c.ExternalFixtureId, FixtureRefreshPurposes.PostMatchStatistics,
                        PostMatchDataPolicy.PerFixtureCooldown, StatisticsDailyCap, nowUtc))
                {
                    statReqs++;
                    var outcome = "NoData";
                    try
                    {
                        var result = await _provider
                            .GetFinishedMatchStatisticsAsync(c.ExternalFixtureId, ct)
                            .ConfigureAwait(false);

                        if (result.Succeeded && result.HasRealData)
                        {
                            var written = await WriteStatisticsAsync(c, result, nowUtc, ct)
                                .ConfigureAwait(false);
                            statRows += written;
                            outcome = written > 0 ? "Applied" : "NoData";
                        }
                        else if (result.Succeeded)
                        {
                            // Cevap geldi ama TEK BİR gerçek ölçüm yok. "Hepsi sıfır"
                            // diye satır yazmak, olmayan veriyi var etmektir.
                            outcome = "NoData";
                        }
                        else
                        {
                            outcome = "ProviderError";
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        outcome = "ProviderError";
                        _logger.LogWarning(ex,
                            "[POST-MATCH DATA] statistics yazimi basarisiz — match {MatchId}", c.MatchId);
                    }

                    _ledger.RecordFixtureAttemptOutcome(
                        c.ExternalFixtureId, FixtureRefreshPurposes.PostMatchStatistics, nowUtc, outcome);
                }
            }

            await _ledger.SaveChangesAsync(ct).ConfigureAwait(false);

            if (eventReqs > 0 || statReqs > 0)
                _logger.LogInformation(
                    "[POST-MATCH DATA] {Cand} aday · events {ER} istek/{ERow} satir · stats {SR} istek/{SRow} satir",
                    candidates.Count, eventReqs, eventRows, statReqs, statRows);

            return new CycleResult(candidates.Count, eventReqs, statReqs, eventRows, statRows);
        }

        /// <summary>Aday maç + hangi verinin eksik olduğu.</summary>
        private sealed record Candidate(
            int MatchId, string ExternalFixtureId, DateTime KickoffUtc,
            int? HomeTeamExternalId, int? AwayTeamExternalId,
            bool HasEvents, bool HasStatistics);

        /// <summary>
        /// ADAYLAR — <see cref="PostMatchDataPolicy.IsCandidate"/> kuralının DB karşılığı.
        ///
        /// Takip edilen maçlar öne alınır: bütçe darken önce kullanıcının beklediği
        /// maçın verisi gelir.
        /// </summary>
        private async Task<List<Candidate>> BuildCandidatesAsync(DateTime nowUtc, CancellationToken ct)
        {
            var locked = LockedCompetitions.All.ToList();
            var since = nowUtc.AddHours(-LookbackHours);

            var rows = await _db.Matches.AsNoTracking()
                .Where(m => m.Status == MatchStatuses.Finished
                         && m.MatchDate >= since && m.MatchDate <= nowUtc
                         && locked.Contains(m.LeagueId)
                         && m.ExternalMatchId != null && m.ExternalMatchId != "")
                .Select(m => new
                {
                    m.Id, m.ExternalMatchId, m.MatchDate, m.LeagueId, m.Status,
                    m.HomeTeamId, m.AwayTeamId,
                    HomeExt = m.HomeTeam != null ? m.HomeTeam.ExternalTeamId : null,
                    AwayExt = m.AwayTeam != null ? m.AwayTeam.ExternalTeamId : null
                })
                .ToListAsync(ct).ConfigureAwait(false);

            if (rows.Count == 0) return new List<Candidate>();

            var matchIds = rows.Select(r => r.Id).ToList();

            var withEvents = new HashSet<int>(await _db.MatchEventRecords.AsNoTracking()
                .Where(e => matchIds.Contains(e.MatchId))
                .Select(e => e.MatchId).Distinct()
                .ToListAsync(ct).ConfigureAwait(false));

            // GERÇEK ÖLÇÜM ARANIR, satır varlığı değil: bütün alanları null olan bir
            // satır "istatistik var" SAYILMAZ (bkz. MatchTeamStatistic.HasAnyMeasurement).
            var statRows = await _db.MatchTeamStatistics.AsNoTracking()
                .Where(s => matchIds.Contains(s.MatchId))
                .ToListAsync(ct).ConfigureAwait(false);
            var withStats = new HashSet<int>(
                statRows.Where(s => s.HasAnyMeasurement).Select(s => s.MatchId).Distinct());

            var followed = new HashSet<int>(await _db.UserMatchFollows.AsNoTracking()
                .Select(f => f.MatchId).Distinct()
                .ToListAsync(ct).ConfigureAwait(false));

            var result = new List<Candidate>();
            foreach (var r in rows)
            {
                var hasEvents = withEvents.Contains(r.Id);
                var hasStats = withStats.Contains(r.Id);

                // İkisi de varsa iş bitmiştir.
                var alreadyHasData = hasEvents && hasStats;

                if (!PostMatchDataPolicy.IsCandidate(
                        r.Status, r.LeagueId, r.ExternalMatchId,
                        alreadyHasData,
                        // Skor kesinliği: Status=Finished bu depoda kesin sonucun
                        // tanımıdır (bkz. SeasonDataCompleteness) — ayrı bir bayrak yok.
                        scoreIsDefinite: true,
                        r.MatchDate, nowUtc))
                    continue;

                result.Add(new Candidate(
                    r.Id, r.ExternalMatchId!, r.MatchDate,
                    ParseExt(r.HomeExt), ParseExt(r.AwayExt),
                    hasEvents, hasStats));
            }

            return result
                .OrderByDescending(c => followed.Contains(c.MatchId))
                .ThenByDescending(c => c.KickoffUtc)
                .ToList();
        }

        private static int? ParseExt(string? raw)
            => int.TryParse(raw, out var v) ? v : null;

        /// <summary>
        /// OLAYLARI YAZ — duplicate YAZILMAZ.
        ///
        /// Kararlı anahtar (<see cref="PostMatchEventKey"/>) hem bellek içinde hem de
        /// DB'de mevcut olanlara karşı kontrol edilir. Bellek kontrolü, aynı yanıtta
        /// birebir aynı iki satır gelirse (sağlayıcı zaman zaman tekrarlar) ikincisini
        /// eler; DB kontrolü ise ikinci bir turu eler. Benzersiz indeks son emniyettir.
        /// </summary>
        private async Task<int> WriteEventsAsync(
            Candidate c, SportsMatchEventsResult result, DateTime nowUtc, CancellationToken ct)
        {
            if (result.Events.Count == 0) return 0;

            var existing = new HashSet<string>(await _db.MatchEventRecords.AsNoTracking()
                .Where(e => e.MatchId == c.MatchId)
                .Select(e => e.ProviderEventId)
                .ToListAsync(ct).ConfigureAwait(false), StringComparer.Ordinal);

            var toAdd = new List<MatchEventRecord>();
            foreach (var e in result.Events)
            {
                var key = PostMatchEventKey.Build(c.ExternalFixtureId, e);
                if (!existing.Add(key)) continue;      // aynı turda veya DB'de zaten var

                toAdd.Add(new MatchEventRecord
                {
                    MatchId           = c.MatchId,
                    ExternalFixtureId = c.ExternalFixtureId,
                    ProviderEventId   = key,
                    Minute            = e.Minute,
                    ExtraMinute       = e.ExtraMinute,
                    TeamExternalId    = e.TeamExternalId,
                    TeamName          = e.TeamName,
                    PlayerName        = e.PlayerName,
                    AssistName        = e.AssistName,
                    EventType         = e.EventType,
                    Detail            = e.Detail,
                    Comments          = e.Comments,
                    Source            = "api-football",
                    FetchedAtUtc      = nowUtc
                });
            }

            if (toAdd.Count == 0) return 0;

            _db.MatchEventRecords.AddRange(toAdd);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return toAdd.Count;
        }

        /// <summary>
        /// İSTATİSTİKLERİ YAZ — maç+taraf başına TEK satır (varsa güncellenir).
        ///
        /// TARAF ÇÖZÜMÜ sağlayıcı takım kimliğinden yapılır, dizi sırasından DEĞİL:
        /// api-football pratikte index 0'ı ev sahibi verir ama bu bir sözleşme değildir.
        /// Kimlik eşleşmezse sıraya düşülür ve bu durum kayda geçer.
        /// </summary>
        private async Task<int> WriteStatisticsAsync(
            Candidate c, SportsMatchStatisticsResult result, DateTime nowUtc, CancellationToken ct)
        {
            var existing = await _db.MatchTeamStatistics
                .Where(s => s.MatchId == c.MatchId)
                .ToListAsync(ct).ConfigureAwait(false);

            var written = 0;
            for (var i = 0; i < result.Teams.Count; i++)
            {
                var t = result.Teams[i];
                if (!t.HasAnyMeasurement) continue;   // boş taraf yazılmaz

                var side = ResolveSide(t.TeamExternalId, c, i);
                if (side == null) continue;

                var row = existing.FirstOrDefault(s => s.Side == side);
                if (row == null)
                {
                    row = new MatchTeamStatistic
                    {
                        MatchId = c.MatchId,
                        ExternalFixtureId = c.ExternalFixtureId,
                        Side = side
                    };
                    _db.MatchTeamStatistics.Add(row);
                    existing.Add(row);
                }

                row.TeamExternalId  = t.TeamExternalId;
                row.TeamName        = t.TeamName;
                row.BallPossession  = t.BallPossession;
                row.TotalShots      = t.TotalShots;
                row.ShotsOnTarget   = t.ShotsOnTarget;
                row.ShotsOffTarget  = t.ShotsOffTarget;
                row.BlockedShots    = t.BlockedShots;
                row.Corners         = t.Corners;
                row.Offsides        = t.Offsides;
                row.Fouls           = t.Fouls;
                row.YellowCards     = t.YellowCards;
                row.RedCards        = t.RedCards;
                row.GoalkeeperSaves = t.GoalkeeperSaves;
                row.TotalPasses     = t.TotalPasses;
                row.AccuratePasses  = t.AccuratePasses;
                row.PassAccuracy    = t.PassAccuracy;
                row.Source          = "api-football";
                row.FetchedAtUtc    = nowUtc;
                written++;
            }

            if (written > 0) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return written;
        }

        /// <summary>Sağlayıcı takım kimliğinden taraf; çözülemezse dizi sırasına düşer.</summary>
        private static string? ResolveSide(int? teamExternalId, Candidate c, int index)
        {
            if (teamExternalId.HasValue)
            {
                if (teamExternalId == c.HomeTeamExternalId) return "Home";
                if (teamExternalId == c.AwayTeamExternalId) return "Away";
            }
            return index switch { 0 => "Home", 1 => "Away", _ => null };
        }
    }
}
