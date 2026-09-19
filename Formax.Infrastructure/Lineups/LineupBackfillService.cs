using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.OfficialSources;
using Formax.Infrastructure.OfficialSources.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Lineups
{
    /// <summary>Tek doldurma koşusunun isteği. Hiçbir alanı varsayılan olarak "sınırsız" değildir.</summary>
    /// <param name="LeagueIds">Boşsa kadro kaynağı doğrulanmış BÜTÜN ligler.</param>
    /// <param name="SeasonIds">Boşsa kaynağın verdiği sezonlar (en yeniden eskiye).</param>
    /// <param name="MaxMatches">Bu koşuda işlenecek EN FAZLA maç (zorunlu tavan).</param>
    /// <param name="DryRun">true: hiçbir şey YAZILMAZ; yalnız okuma, eşleme ve doğrulama ölçülür.</param>
    /// <param name="IncludeMinutes">Kaynak destekliyorsa gerçek değişiklik dakikaları da çekilsin mi (maç başına +1 istek).</param>
    public sealed record LineupBackfillRequest(
        IReadOnlyList<int>? LeagueIds = null,
        IReadOnlyList<string>? SeasonIds = null,
        int MaxMatches = 25,
        bool DryRun = true,
        bool IncludeMinutes = true);

    public sealed class LineupBackfillSeasonReport
    {
        public string SourceKey { get; set; } = string.Empty;
        public int LeagueId { get; set; }
        public string SeasonId { get; set; } = string.Empty;
        public string SeasonLabel { get; set; } = string.Empty;
        /// <summary>Kaynağın sezonda listelediği maç.</summary>
        public int SourceMatches { get; set; }
        /// <summary>DB'de canonical (bitmiş) karşılığı bulunan maç.</summary>
        public int CanonicalMatches { get; set; }
        /// <summary>Bu koşuda gerçekten işlenen maç.</summary>
        public int Processed { get; set; }
        public int Verified { get; set; }
        public int Partial { get; set; }
        public int NotPublished { get; set; }
        public int Rejected { get; set; }
        public int IdentityRejected { get; set; }
        public int NoCanonicalFixture { get; set; }
        public int FetchFailed { get; set; }
        public int Unchanged { get; set; }
        public int WithMinutes { get; set; }
        /// <summary>Kaynakta olup DB'de canonical fikstürü OLMAYAN maçlar (bu görevde eklenmez, raporlanır).</summary>
        public List<string> MissingFixtures { get; set; } = new();
        public string Status { get; set; } = LineupBackfillStatuses.Pending;
        public string? Error { get; set; }
    }

    public sealed class LineupBackfillReport
    {
        public bool DryRun { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public double DurationSeconds { get; set; }
        public int Seasons { get; set; }
        public int Processed { get; set; }
        public int Verified { get; set; }
        public int Unchanged { get; set; }
        public int Failed { get; set; }
        /// <summary>Bu koşuda resmî kaynaklara çıkan GERÇEK istek sayısı (kaynak defterinden sayılır).</summary>
        public int SourceRequests { get; set; }
        public int MaxMatches { get; set; }
        /// <summary>Tavana takılıp bırakıldıysa true — bir sonraki koşu kaldığı yerden devam eder.</summary>
        public bool StoppedAtLimit { get; set; }
        public List<LineupBackfillSeasonReport> Seasons_ { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }

    /// <summary>
    /// GEÇMİŞ KADRO DOLDURMA — resmî kaynakların tamamlanmış sezonlarındaki ilk 11'leri canonical
    /// depoya yazar. API-FOOTBALL'A HİÇBİR İSTEK ATMAZ.
    ///
    /// TASARIM
    ///  • Canlı toplama mimarisi DEĞİŞMEZ: doğrulama ve yazma
    ///    <see cref="OfficialLineupCollector.WriteHistoricalAsync"/> ile AYNI yoldan geçer.
    ///  • Checkpoint kaynak × sezon başınadır; süreç durdurulup yeniden başlatıldığında iş
    ///    kaldığı yerden devam eder (uygulama açılışında KENDİLİĞİNDEN başlamaz).
    ///  • Idempotens: her maç için tek deneme satırı tutulur; aynı içerik özeti (ContentHash)
    ///    ikinci kez işlenmez, değişmişse kontrollü güncellenir.
    ///  • Hız: dış istekler tek kapıdan (fetcher) geçer ve host başına sıralı + en az 1,5 sn
    ///    aralıklıdır; üstüne maç başına ek bekleme konulabilir. Paralellik YOKTUR.
    ///  • Tavan: her koşu <see cref="LineupBackfillRequest.MaxMatches"/> maçta durur.
    ///  • Başarısızlık: maç başına deneme sayacı; <see cref="LineupBackfillPolicyLimits.MaxAttempts"/>
    ///    sonrası bir daha denenmez (sonsuz döngü yok).
    ///  • Kaynakta olup DB'de canonical fikstürü olmayan maç EKLENMEZ, yalnız raporlanır.
    /// </summary>
    public sealed class LineupBackfillService
    {
        /// <summary>Maç başına ek bekleme (fetcher'ın host kapısına EKtir). Ayar: OfficialSources:Backfill:MatchDelayMs.</summary>
        public static readonly TimeSpan DefaultMatchDelay = TimeSpan.FromMilliseconds(250);

        /// <summary>Aynı anda tek koşu — iki doldurma birbirinin checkpoint'ini bozamaz.</summary>
        private static readonly SemaphoreSlim RunLock = new(1, 1);

        /// <summary>Çalışan koşuyu dışarıdan durdurma bayrağı (admin ucu).</summary>
        private static volatile bool _stopRequested;

        private readonly FormaxDbContext _db;
        private readonly IReadOnlyDictionary<string, IOfficialCompetitionSource> _sources;
        private readonly OfficialLineupCollector _collector;
        private readonly IConfiguration _config;
        private readonly ILogger<LineupBackfillService> _log;

        public LineupBackfillService(
            FormaxDbContext db,
            IEnumerable<IOfficialCompetitionSource> sources,
            OfficialLineupCollector collector,
            IConfiguration config,
            ILogger<LineupBackfillService> log)
        {
            _db = db;
            _sources = sources.ToDictionary(s => s.SourceKey, StringComparer.Ordinal);
            _collector = collector;
            _config = config;
            _log = log;
        }

        public static bool IsRunning => RunLock.CurrentCount == 0;
        public static void RequestStop() => _stopRequested = true;

        /// <summary>Kadro kaynağı DOĞRULANMIŞ ve geçmiş listeleme yeteneği OLAN ligler.</summary>
        public IReadOnlyList<(int LeagueId, IOfficialHistoricalLineupSource Source)> SupportedLeagues()
        {
            var list = new List<(int, IOfficialHistoricalLineupSource)>();
            foreach (var leagueId in OfficialSourceRegistry.LockedLeagueIds)
            {
                var descriptor = OfficialSourceRegistry.VerifiedFor(leagueId, OfficialPurposes.Lineup).FirstOrDefault();
                if (descriptor == null) continue;
                if (_sources.GetValueOrDefault(descriptor.Key) is IOfficialHistoricalLineupSource h)
                    list.Add((leagueId, h));
            }
            return list;
        }

        public async Task<LineupBackfillReport> RunAsync(LineupBackfillRequest request, CancellationToken ct = default)
        {
            if (!await RunLock.WaitAsync(0, ct).ConfigureAwait(false))
                return new LineupBackfillReport { Notes = { "Zaten çalışan bir doldurma koşusu var." } };

            _stopRequested = false;
            var sw = Stopwatch.StartNew();
            var startedAt = DateTime.UtcNow;
            // Bu koşunun tur anahtarı öneki — istek sayımı buna göre yapılır (zaman penceresine DEĞİL).
            var runKeyPrefix = $"backfill:{startedAt:yyyyMMddHHmmss}:";
            var report = new LineupBackfillReport
            {
                DryRun = request.DryRun, StartedAtUtc = startedAt, MaxMatches = request.MaxMatches
            };

            try
            {
                var delay = TimeSpan.FromMilliseconds(
                    _config.GetValue("OfficialSources:Backfill:MatchDelayMs", (int)DefaultMatchDelay.TotalMilliseconds));
                var budget = Math.Max(1, request.MaxMatches);

                foreach (var (leagueId, source) in SupportedLeagues())
                {
                    if (request.LeagueIds is { Count: > 0 } && !request.LeagueIds.Contains(leagueId)) continue;
                    if (budget <= 0 || _stopRequested) break;

                    var round = new OfficialRoundContext(
                        runKeyPrefix + leagueId, startedAt, OfficialPurposes.Lineup);

                    var seasonsRead = await source.ReadSeasonsAsync(round, ct).ConfigureAwait(false);
                    if (!seasonsRead.Ok || seasonsRead.Value == null)
                    {
                        report.Notes.Add($"lig {leagueId}: sezon listesi okunamadı ({seasonsRead.Detail ?? seasonsRead.Outcome})");
                        continue;
                    }

                    foreach (var season in seasonsRead.Value.OrderByDescending(s => s.StartYear))
                    {
                        if (budget <= 0 || _stopRequested) break;
                        if (request.SeasonIds is { Count: > 0 } && !request.SeasonIds.Contains(season.SeasonId)) continue;

                        var seasonReport = await RunSeasonAsync(
                            leagueId, source, season, round, request, delay, budget, ct).ConfigureAwait(false);
                        report.Seasons_.Add(seasonReport);
                        budget -= seasonReport.Processed;
                        report.Processed += seasonReport.Processed;
                        report.Verified += seasonReport.Verified;
                        report.Unchanged += seasonReport.Unchanged;
                        report.Failed += seasonReport.FetchFailed + seasonReport.Rejected + seasonReport.IdentityRejected;
                    }
                }

                report.Seasons = report.Seasons_.Count;
                report.StoppedAtLimit = budget <= 0;
                if (_stopRequested) report.Notes.Add("Koşu dışarıdan durduruldu; checkpoint korundu.");
            }
            finally
            {
                report.DurationSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
                report.SourceRequests = await CountRequestsAsync(runKeyPrefix, ct).ConfigureAwait(false);
                RunLock.Release();
            }

            _log.LogInformation(
                "[LINEUP BACKFILL] {Mode}: sezon={Seasons} işlenen={Processed} doğrulanan={Verified} " +
                "değişmeyen={Unchanged} başarısız={Failed} istek={Requests} süre={Seconds}sn",
                request.DryRun ? "DRY-RUN" : "YAZIM", report.Seasons, report.Processed, report.Verified,
                report.Unchanged, report.Failed, report.SourceRequests, report.DurationSeconds);
            return report;
        }

        // ════════════════════════════════════════════════════════════════════════════
        private async Task<LineupBackfillSeasonReport> RunSeasonAsync(
            int leagueId, IOfficialHistoricalLineupSource source, OfficialSeason season,
            OfficialRoundContext round, LineupBackfillRequest request, TimeSpan delay, int budget, CancellationToken ct)
        {
            var r = new LineupBackfillSeasonReport
            {
                SourceKey = source.SourceKey, LeagueId = leagueId,
                SeasonId = season.SeasonId, SeasonLabel = season.Label
            };

            // TAMAMLANMIŞ SEZON yeniden taranmaz — operatör sezonu AÇIKÇA hedeflemedikçe.
            // Açıkça hedeflemek kontrollü yeniden işlemenin tek yoludur (kaynak düzeltmesi vb.).
            var explicitlyTargeted = request.SeasonIds is { Count: > 0 } && request.SeasonIds.Contains(season.SeasonId);
            var checkpoint = request.DryRun ? null : await UpsertCheckpointAsync(source.SourceKey, leagueId, season, ct).ConfigureAwait(false);
            if (checkpoint is { Status: LineupBackfillStatuses.Completed } && !explicitlyTargeted)
            {
                r.Status = LineupBackfillStatuses.Completed;
                return r;
            }

            var read = await source.ReadSeasonMatchesAsync(season, round, ct).ConfigureAwait(false);
            if (!read.Ok || read.Value == null)
            {
                r.Status = LineupBackfillStatuses.Failed;
                r.Error = read.Detail ?? read.Outcome;
                if (checkpoint != null) await FailCheckpointAsync(checkpoint, r.Error, ct).ConfigureAwait(false);
                return r;
            }

            var sourceMatches = read.Value.Where(m => m.Status != OfficialMatchStatuses.Scheduled).ToList();
            r.SourceMatches = sourceMatches.Count;

            // Canonical fikstürler: bu ligin BİTMİŞ maçları. Kaynak listesindeki maç burada
            // bulunamazsa EKLENMEZ — yalnız raporlanır (görev kapsamı dışı).
            var canonical = await _db.Matches.AsNoTracking()
                .Where(m => m.LeagueId == leagueId && m.Status == MatchStatuses.Finished)
                .Select(m => new { m.Id, m.MatchDate, m.HomeTeamId, m.AwayTeamId, Home = m.HomeTeam!.Name, Away = m.AwayTeam!.Name })
                .ToListAsync(ct).ConfigureAwait(false);

            var attempts = (await _db.LineupBackfillAttempts.AsNoTracking()
                    .Where(a => a.SourceKey == source.SourceKey && a.SeasonId == season.SeasonId)
                    .ToListAsync(ct).ConfigureAwait(false))
                .ToDictionary(a => a.OfficialMatchId, StringComparer.Ordinal);

            var processed = 0;
            foreach (var sourceMatch in sourceMatches)
            {
                if (processed >= budget || _stopRequested) break;
                ct.ThrowIfCancellationRequested();

                // Zaten tamamlanmış ya da deneme tavanını aşmış maç yeniden İŞLENMEZ (idempotens).
                if (attempts.TryGetValue(sourceMatch.OfficialMatchId, out var prior))
                {
                    if (prior.Outcome is "Verified" or "Partial" or "NoCanonicalFixture") { r.Unchanged++; continue; }
                    if (prior.Attempts >= LineupBackfillPolicyLimits.MaxAttempts) { r.Unchanged++; continue; }
                }

                // ADIM 1 — UCUZ ÖN ELEME: yalnız YÖN (ev/deplasman adları). Hiç aday yoksa
                // kaynağa istek ÜRETİLMEZ; maç DB'de yok diye raporlanır.
                var candidates = canonical
                    .Where(c => OfficialMatchIdentityResolver.HomeMatches(sourceMatch, c.Home ?? string.Empty)
                             && OfficialMatchIdentityResolver.AwayMatches(sourceMatch, c.Away ?? string.Empty))
                    .Select(c => (c.Id, c.MatchDate, c.Home, c.Away))
                    .ToList();
                if (candidates.Count == 0)
                {
                    r.NoCanonicalFixture++;
                    if (r.MissingFixtures.Count < 25)
                        r.MissingFixtures.Add($"{sourceMatch.HomeName}–{sourceMatch.AwayName} ({sourceMatch.OfficialMatchId})");
                    if (!request.DryRun)
                        await RecordAttemptAsync(source.SourceKey, season.SeasonId, leagueId, sourceMatch.OfficialMatchId,
                            null, "NoCanonicalFixture", null, null, ct).ConfigureAwait(false);
                    continue;
                }

                // ADIM 2 — SAATİ OLMAYAN KAYNAK (TFF sezon tablosu tarih yayımlamıyor): başlama
                // saati maçın KENDİ resmî sayfasından gelir. Aynı istek kadroyu da getirir, bu
                // yüzden ek maliyet YOKTUR. Kimlik kuralı GEVŞETİLMEZ: karar yine ad + tarih.
                OfficialLineupDocument? preloaded = null;
                var hydrated = sourceMatch;
                if (sourceMatch.KickoffUtc == null && source is TffSource tffSource)
                {
                    processed++;
                    var page = await tffSource.ReadMatchWithLineupAsync(
                        sourceMatch, round with { MatchId = null }, ct).ConfigureAwait(false);
                    if (!page.Ok)
                    {
                        r.Processed++; r.FetchFailed++;
                        if (!request.DryRun)
                            await RecordAttemptAsync(source.SourceKey, season.SeasonId, leagueId, sourceMatch.OfficialMatchId,
                                null, "FetchFailed", null, page.Detail ?? page.Outcome, ct).ConfigureAwait(false);
                        if (delay > TimeSpan.Zero) await Task.Delay(delay, ct).ConfigureAwait(false);
                        continue;
                    }
                    hydrated = page.Value.Record;
                    preloaded = page.Value.Lineup;
                }

                var match = ResolveCanonical(hydrated, candidates, leagueId);
                if (match == null)
                {
                    if (preloaded == null) processed++;
                    r.Processed++;
                    r.IdentityRejected++;
                    if (!request.DryRun)
                        await RecordAttemptAsync(source.SourceKey, season.SeasonId, leagueId, sourceMatch.OfficialMatchId,
                            null, "IdentityRejected", null, "ad eşleşti, başlama saati penceresi tutmadı", ct).ConfigureAwait(false);
                    if (delay > TimeSpan.Zero) await Task.Delay(delay, ct).ConfigureAwait(false);
                    continue;
                }

                if (preloaded == null) processed++;
                r.Processed++;
                var outcome = await ProcessMatchAsync(source, season, round, hydrated, match.Value, request, r, preloaded, ct)
                    .ConfigureAwait(false);

                if (!request.DryRun)
                {
                    await RecordAttemptAsync(source.SourceKey, season.SeasonId, leagueId, sourceMatch.OfficialMatchId,
                        match.Value.MatchId, outcome.Outcome, outcome.ContentHash, outcome.Error, ct).ConfigureAwait(false);
                    if (checkpoint != null)
                    {
                        checkpoint.ProcessedMatches++;
                        checkpoint.VerifiedMatches += outcome.Outcome == "Verified" ? 1 : 0;
                        checkpoint.FailedMatches += outcome.Outcome is "FetchFailed" or "Rejected" ? 1 : 0;
                        checkpoint.LastOfficialMatchId = sourceMatch.OfficialMatchId;
                        checkpoint.LastRunAtUtc = DateTime.UtcNow;
                        checkpoint.SourceMatches = r.SourceMatches;
                        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                }

                if (delay > TimeSpan.Zero) await Task.Delay(delay, ct).ConfigureAwait(false);
            }

            r.CanonicalMatches = canonical.Count;
            if (checkpoint != null)
            {
                // Sezon ancak BAŞTAN SONA gezildiyse VE yeniden denenebilir başarısız maç
                // kalmadıysa tamamlanmış sayılır. Aksi hâlde Running kalır ve sonraki koşu
                // kalanları dener (deneme tavanına ulaşanlar kendiliğinden düşer).
                var retryable = await _db.LineupBackfillAttempts.AsNoTracking().CountAsync(
                    a => a.SourceKey == source.SourceKey && a.SeasonId == season.SeasonId
                         && a.Attempts < LineupBackfillPolicyLimits.MaxAttempts
                         && (a.Outcome == "FetchFailed" || a.Outcome == "Rejected" || a.Outcome == "NotPublished"
                             || a.Outcome == "IdentityRejected"), ct).ConfigureAwait(false);
                var finished = processed < budget && !_stopRequested && retryable == 0;
                checkpoint.Status = finished ? LineupBackfillStatuses.Completed : LineupBackfillStatuses.Running;
                checkpoint.CompletedAtUtc = finished ? DateTime.UtcNow : null;
                checkpoint.LastError = null;
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                r.Status = checkpoint.Status;
            }
            else r.Status = request.DryRun ? "DryRun" : LineupBackfillStatuses.Running;

            return r;
        }

        private sealed record ProcessOutcome(string Outcome, string? ContentHash, string? Error);

        private async Task<ProcessOutcome> ProcessMatchAsync(
            IOfficialHistoricalLineupSource source, OfficialSeason season, OfficialRoundContext round,
            OfficialMatchRecord sourceMatch, (int MatchId, string Home, string Away) match,
            LineupBackfillRequest request, LineupBackfillSeasonReport r,
            OfficialLineupDocument? preloaded, CancellationToken ct)
        {
            var matchRound = round with { MatchId = match.MatchId };

            OfficialLineupDocument? doc;
            if (preloaded != null) doc = preloaded; // TFF: sayfa kimlik adımında zaten indirildi
            else
            {
                // Kadro okuması kaynağa göre: Serie A sezon kökünü, diğerleri kendi ucunu kullanır.
                OfficialRead<OfficialLineupDocument> read = source switch
                {
                    SerieASdpSource serieA => await serieA.ReadLineupForSeasonAsync(sourceMatch, season.SeasonId, matchRound, ct).ConfigureAwait(false),
                    TffSource tff => await ReadTffAsync(tff, sourceMatch, matchRound, ct).ConfigureAwait(false),
                    IOfficialCompetitionSource competition => await competition.ReadLineupAsync(sourceMatch, matchRound, ct).ConfigureAwait(false),
                    _ => new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.NotSupported, null, null)
                };
                if (!read.Ok) { r.FetchFailed++; return new("FetchFailed", null, read.Detail ?? read.Outcome); }
                doc = read.Value;
            }

            if (doc == null) { r.NotPublished++; return new("NotPublished", null, null); }

            // GERÇEK DEĞİŞİKLİK DAKİKALARI — yalnız kaynak ayrı bir uçla yayımlıyorsa (+1 istek).
            IReadOnlyList<OfficialSubstitution>? substitutions = null;
            if (request.IncludeMinutes && source is IOfficialParticipationSource participation)
            {
                var part = await participation.ReadParticipationAsync(sourceMatch, matchRound, ct).ConfigureAwait(false);
                if (part.Ok && part.Value != null) substitutions = part.Value.Substitutions;
            }
            if (OfficialLineupCollector.DataQualityOf(doc, substitutions) == "WithMinutes") r.WithMinutes++;

            if (request.DryRun)
            {
                var verdict = OfficialContentVerificationService.VerifyLineup(doc, match.Home, match.Away);
                if (!verdict.AnySideAccepted) { r.Rejected++; return new("Rejected", doc.ContentHash, verdict.SourceRejectReason); }
                var complete = verdict.Home.Accepted && verdict.Away.Accepted;
                if (complete) r.Verified++; else r.Partial++;
                return new(complete ? "Verified" : "Partial", doc.ContentHash, null);
            }

            var written = await _collector.WriteHistoricalAsync(
                match.MatchId, match.Home, match.Away, doc, substitutions, DateTime.UtcNow, ct).ConfigureAwait(false);

            switch (written.Outcome)
            {
                case LineupOutcomes.Released: r.Verified++; return new("Verified", doc.ContentHash, null);
                case LineupOutcomes.PartiallyReleased: r.Partial++; return new("Partial", doc.ContentHash, null);
                default: r.Rejected++; return new("Rejected", doc.ContentHash, written.Detail);
            }
        }

        /// <summary>TFF: kadro ve başlama saati AYNI sayfadadır → maç başına tek istek.</summary>
        private static async Task<OfficialRead<OfficialLineupDocument>> ReadTffAsync(
            TffSource tff, OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct)
        {
            var page = await tff.ReadMatchWithLineupAsync(match, round, ct).ConfigureAwait(false);
            if (!page.Ok) return new(null, page.Outcome, page.Detail, page.Fetch);
            return new(page.Value.Lineup, OfficialReadOutcomes.Ok, null, page.Fetch);
        }

        /// <summary>
        /// CANONICAL EŞLEME — resmî kaynak kaydı ile FORMAX fikstürü. Yalnız metin benzerliği
        /// KULLANILMAZ: <see cref="OfficialMatchIdentityResolver"/> ad eşleşmesini tarih yakınlığıyla
        /// birlikte değerlendirir ve belirsiz eşleşmeyi REDDEDER.
        /// </summary>
        private static (int MatchId, string Home, string Away)? ResolveCanonical(
            OfficialMatchRecord sourceMatch, IReadOnlyList<(int Id, DateTime Date, string? Home, string? Away)> canonical, int leagueId)
        {
            if (sourceMatch.KickoffUtc == null) return null; // saat yoksa kimlik KURULMAZ
            foreach (var c in canonical)
            {
                var identity = OfficialMatchIdentityResolver.Resolve(
                    new FormaxMatchIdentity(c.Id, leagueId, c.Home ?? string.Empty, c.Away ?? string.Empty, c.Date),
                    new[] { sourceMatch });
                if (identity.Accepted) return (c.Id, c.Home ?? string.Empty, c.Away ?? string.Empty);
            }
            return null;
        }

        private async Task<LineupBackfillCheckpoint> UpsertCheckpointAsync(
            string sourceKey, int leagueId, OfficialSeason season, CancellationToken ct)
        {
            var cp = await _db.LineupBackfillCheckpoints
                .FirstOrDefaultAsync(c => c.SourceKey == sourceKey && c.SeasonId == season.SeasonId, ct).ConfigureAwait(false);
            if (cp == null)
            {
                cp = new LineupBackfillCheckpoint
                {
                    SourceKey = sourceKey, LeagueId = leagueId, SeasonId = season.SeasonId,
                    SeasonLabel = season.Label, Status = LineupBackfillStatuses.Running,
                    CreatedAtUtc = DateTime.UtcNow, StartedAtUtc = DateTime.UtcNow
                };
                _db.LineupBackfillCheckpoints.Add(cp);
            }
            else if (cp.Status != LineupBackfillStatuses.Completed)
            {
                cp.Status = LineupBackfillStatuses.Running;
                cp.StartedAtUtc ??= DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return cp;
        }

        private async Task FailCheckpointAsync(LineupBackfillCheckpoint cp, string? error, CancellationToken ct)
        {
            cp.Status = LineupBackfillStatuses.Failed;
            cp.LastError = error is { Length: > 500 } ? error[..500] : error;
            cp.LastRunAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        private async Task RecordAttemptAsync(
            string sourceKey, string seasonId, int leagueId, string officialMatchId, int? matchId,
            string outcome, string? contentHash, string? error, CancellationToken ct)
        {
            var row = await _db.LineupBackfillAttempts
                .FirstOrDefaultAsync(a => a.SourceKey == sourceKey && a.OfficialMatchId == officialMatchId, ct).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            if (row == null)
            {
                _db.LineupBackfillAttempts.Add(new LineupBackfillAttempt
                {
                    SourceKey = sourceKey, OfficialMatchId = officialMatchId, MatchId = matchId, LeagueId = leagueId,
                    SeasonId = seasonId, Outcome = outcome, ContentHash = contentHash, Attempts = 1,
                    FirstAttemptAtUtc = now, LastAttemptAtUtc = now,
                    LastError = error is { Length: > 500 } ? error[..500] : error
                });
            }
            else
            {
                row.Outcome = outcome;
                row.MatchId = matchId ?? row.MatchId;
                row.ContentHash = contentHash ?? row.ContentHash;
                row.Attempts++;
                row.LastAttemptAtUtc = now;
                row.LastError = error is { Length: > 500 } ? error[..500] : error;
            }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Kaynak defterinden BU KOŞUYA ait istek sayısı. Zaman penceresiyle saymak yanlıştır:
        /// aynı anda çalışan canlı işlerin (ve paralel bir doldurma koşusunun) istekleri sayıya
        /// karışırdı. Sayım tur anahtarına (RoundKey) göre yapılır.
        /// </summary>
        private Task<int> CountRequestsAsync(string roundKeyPrefix, CancellationToken ct)
            => _db.OfficialSourceFetches.AsNoTracking()
                .CountAsync(f => f.RoundKey != null && f.RoundKey.StartsWith(roundKeyPrefix), ct);
    }
}
