using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public sealed record VideoQueueCycleResult(int EnqueuedRecent, int EnqueuedBackfill, int Processed, IReadOnlyList<string> Outcomes,
        int HttpUsed = 0, string? StopReason = null);

    /// <summary>
    /// KALICI VİDEO KEŞİF KUYRUĞU — kullanıcı sayfası açmadan çalışır; restart kuyruğu, deneme sayısını ve
    /// sonraki deneme anını kaybetmez (hepsi DB'de).
    ///
    /// KAPSAM (15.09.2026): kilitli 11 organizasyonun BÜTÜN sonuçlanmış maçları — ufuk sınırı YOK. Bugün/dün biten maçlar
    /// her turda doğrudan alınır; daha eskiler kalıcı keyset imleciyle (<see cref="VideoDiscoveryCursor"/>) sayfa sayfa
    /// kuyruğa girer, tablo belleğe yüklenmez. Tur bitince imleç başa döner ve videosu hâlâ olmayan/engellenen maçlar
    /// yeniden kapsama girer.
    ///
    /// ÖNCELİK: bugün → dün → son 7 gün → son 30 gün → içinde bulunulan sezon → eski sezonlar. Seçim iki şeritlidir:
    /// turun çoğu en yeni maçlara, sabit bir payı en uzun süredir bekleyen eski maçlara ayrılır (eski maç aç kalmaz).
    /// Aynı maçı iki süreç aynı anda taramaz: satır atomik UPDATE ile kilitlenir.
    /// </summary>
    public sealed class MatchVideoDiscoveryQueueService
    {
        public static readonly TimeSpan RecentWindow = TimeSpan.FromHours(48);
        public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(10);
        public const string BackfillCursorName = "video-backfill";

        private readonly FormaxDbContext _db;
        private readonly IMatchVideoRegistrar _registrar;
        private readonly IOfficialMatchVideoProvider _provider;
        private readonly IOfficialVideoSourceCatalog _catalog;
        private readonly IConfiguration _config;
        private readonly ILogger<MatchVideoDiscoveryQueueService> _log;
        private readonly Telemetry.VideoDiscoveryRequestLog? _requests;
        private readonly VideoHttpBudget? _budget;
        private readonly string _owner = Environment.MachineName + ":" + Environment.ProcessId;

        public MatchVideoDiscoveryQueueService(FormaxDbContext db, IMatchVideoRegistrar registrar, IOfficialMatchVideoProvider provider,
            IOfficialVideoSourceCatalog catalog, IConfiguration config, ILogger<MatchVideoDiscoveryQueueService> log,
            Telemetry.VideoDiscoveryRequestLog? requests = null, VideoHttpBudget? budget = null)
        {
            _db = db; _registrar = registrar; _provider = provider; _catalog = catalog; _config = config; _log = log; _requests = requests; _budget = budget;
        }

        public async Task<VideoQueueCycleResult> RunCycleAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var (recent, backfill) = await EnqueueAsync(nowUtc, ct).ConfigureAwait(false);
            var max = Math.Max(0, _config.GetValue("PostMatch:Video:MaxMatchesPerCycle", 30));
            var outcomes = new List<string>();
            string? stop = null;
            foreach (var item in await DueAsync(nowUtc, max, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                // Bütçe bitti: kalan maçlar KAYBOLMAZ — NextAttemptUtc değişmediği için sonraki turda ilk sıradadır.
                if (_budget != null && _budget.Remaining <= 0) { stop = "http-budget-exhausted"; break; }
                if (!await TryClaimAsync(item.MatchId, nowUtc, ct).ConfigureAwait(false)) continue;
                outcomes.Add(await ProcessAsync(item.MatchId, nowUtc, ct).ConfigureAwait(false));
            }
            if (recent + backfill + outcomes.Count > 0)
                _log.LogInformation("[VIDEO-QUEUE] kuyruğa: yeni={Recent} backfill={Backfill}; işlenen={Processed} http={Http} dur={Stop}: {Outcomes}",
                    recent, backfill, outcomes.Count, _budget?.Used ?? 0, stop ?? "-", string.Join(", ", outcomes));
            return new VideoQueueCycleResult(recent, backfill, outcomes.Count, outcomes, _budget?.Used ?? 0, stop);
        }

        /// <summary>Bugün/dün biten maçlar + kalıcı imleçten bir sayfa daha eski maç kuyruğa alınır.</summary>
        public async Task<(int Recent, int Backfill)> EnqueueAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var locked = LockedCompetitions.All.ToList();
            var recentFrom = nowUtc - RecentWindow - MatchVideoIdentityValidator.MatchDuration;
            var recent = await _db.Matches.AsNoTracking()
                .Where(m => m.Status == MatchStatuses.Finished && locked.Contains(m.LeagueId)
                            && m.MatchDate >= recentFrom && m.MatchDate <= nowUtc
                            && m.ExternalMatchId != null && m.ExternalMatchId != ""
                            && !_db.MatchVideoDiscoveryQueue.Any(q => q.MatchId == m.Id))
                .OrderByDescending(m => m.MatchDate).Take(200)
                .ToListAsync(ct).ConfigureAwait(false);

            var playableIds = recent.Select(r => r.Id).ToList();
            var playable = await _db.MatchVideos.AsNoTracking()
                .Where(v => v.CanPlayInApp && v.DiscoveryProvenance == MatchVideoRules.OfficialWebProvenance && playableIds.Contains(v.MatchId))
                .Select(v => new { v.MatchId, v.VideoType }).ToListAsync(ct).ConfigureAwait(false);

            foreach (var m in recent)
            {
                var end = MatchVideoIdentityValidator.EndOf(m.MatchDate);
                var full = playable.Any(p => p.MatchId == m.Id && IsFull(p.VideoType));
                var goals = playable.Any(p => p.MatchId == m.Id && IsGoal(p.VideoType));
                var state = VideoDiscoveryStates.Resolve(full, goals, false, false, 0);
                _db.MatchVideoDiscoveryQueue.Add(NewItem(m, end, Bucket(end, nowUtc), nowUtc, state,
                    VideoDiscoveryStates.StopsRetrying(state) ? null : VideoDiscoverySchedule.NextAttempt(end, 0, null)));
            }
            if (recent.Count > 0) await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            var backfill = await EnqueueBackfillPageAsync(nowUtc, recentFrom, ct).ConfigureAwait(false);
            return (recent.Count, backfill);
        }

        /// <summary>
        /// KALICI İMLEÇLE BİR SAYFA — (MatchDate, Id) azalan keyset; sayfa sonunda imleç DB'ye yazılır. Kuyrukta olmayan ve
        /// resmî web kanıtlı oynatılabilir tam özeti olmayan her sonuçlanmış maç alınır; ufuk sınırı yoktur.
        /// </summary>
        public async Task<int> EnqueueBackfillPageAsync(DateTime nowUtc, DateTime recentFrom, CancellationToken ct = default)
        {
            var batch = Math.Max(0, _config.GetValue("PostMatch:Video:BackfillBatch", 400));
            if (batch == 0) return 0;
            var locked = LockedCompetitions.All.ToList();

            var cursor = await _db.VideoDiscoveryCursors.FirstOrDefaultAsync(c => c.Name == BackfillCursorName, ct).ConfigureAwait(false);
            if (cursor == null)
            {
                cursor = new VideoDiscoveryCursor { Name = BackfillCursorName, UpdatedAtUtc = nowUtc, PassStartedAtUtc = nowUtc };
                _db.VideoDiscoveryCursors.Add(cursor);
            }

            var q = _db.Matches.AsNoTracking()
                .Where(m => m.Status == MatchStatuses.Finished && locked.Contains(m.LeagueId)
                            && m.MatchDate < recentFrom
                            && m.ExternalMatchId != null && m.ExternalMatchId != "");
            if (cursor.LastMatchDateUtc is DateTime lastDate && cursor.LastMatchId is int lastId)
                q = q.Where(m => m.MatchDate < lastDate || (m.MatchDate == lastDate && m.Id < lastId));

            var page = await q.OrderByDescending(m => m.MatchDate).ThenByDescending(m => m.Id).Take(batch)
                .ToListAsync(ct).ConfigureAwait(false);

            var enqueued = 0;
            if (page.Count > 0)
            {
                var ids = page.Select(m => m.Id).ToList();
                var queued = (await _db.MatchVideoDiscoveryQueue.AsNoTracking().Where(x => ids.Contains(x.MatchId)).Select(x => x.MatchId)
                    .ToListAsync(ct).ConfigureAwait(false)).ToHashSet();
                var withFull = (await _db.MatchVideos.AsNoTracking()
                    .Where(v => ids.Contains(v.MatchId) && v.CanPlayInApp && v.DiscoveryProvenance == MatchVideoRules.OfficialWebProvenance
                                && (v.VideoType == MatchVideoTypes.MatchHighlights || v.VideoType == MatchVideoTypes.ExtendedHighlights))
                    .Select(v => v.MatchId).ToListAsync(ct).ConfigureAwait(false)).ToHashSet();

                foreach (var m in page)
                {
                    if (queued.Contains(m.Id) || withFull.Contains(m.Id)) continue;
                    var end = MatchVideoIdentityValidator.EndOf(m.MatchDate);
                    // Geçmiş maç: ilk günün planı çoktan geçti; "aranıyor" değil, "henüz bulunamadı" + hemen deneme.
                    _db.MatchVideoDiscoveryQueue.Add(NewItem(m, end, Bucket(end, nowUtc), nowUtc, VideoDiscoveryStates.NotAvailableYet, nowUtc));
                    enqueued++;
                }
                var last = page[^1];
                cursor.LastMatchDateUtc = last.MatchDate;
                cursor.LastMatchId = last.Id;
            }
            cursor.ScannedTotal += page.Count;
            cursor.EnqueuedTotal += enqueued;
            cursor.UpdatedAtUtc = nowUtc;
            if (page.Count < batch)
            {
                // Tur tamam: imleç başa döner; bir sonraki geçiş yeni biten ve hâlâ videosuz kalan maçları yeniden görür.
                cursor.Pass++;
                cursor.LastMatchDateUtc = null;
                cursor.LastMatchId = null;
                cursor.LastPassCompletedAtUtc = nowUtc;
                cursor.PassStartedAtUtc = nowUtc;
            }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return enqueued;
        }

        /// <summary>
        /// Zamanı gelen satırlar — iki şerit: (1) en yeni maçlar (bitiş anı azalan: bugün → dün → 7 gün → …), (2) en uzun
        /// süredir bekleyen eski maçlar (NextAttemptUtc artan). İkinci şeridin payı <c>PostMatch:Video:OldLaneShare</c>.
        /// </summary>
        public async Task<List<MatchVideoDiscoveryQueueItem>> DueAsync(DateTime nowUtc, int max, CancellationToken ct = default)
        {
            if (max <= 0) return new List<MatchVideoDiscoveryQueueItem>();
            var due = _db.MatchVideoDiscoveryQueue.AsNoTracking()
                .Where(q => q.NextAttemptUtc != null && q.NextAttemptUtc <= nowUtc
                            && (q.LockedUntilUtc == null || q.LockedUntilUtc < nowUtc));
            var oldShare = Math.Clamp(_config.GetValue("PostMatch:Video:OldLaneShare", 0.3), 0.0, 1.0);
            var oldSlots = max > 1 ? Math.Max(1, (int)Math.Round(max * oldShare)) : 0;

            var newest = await due.OrderByDescending(q => q.EndUtc).Take(max).ToListAsync(ct).ConfigureAwait(false);
            var monthAgo = nowUtc.AddDays(-30);
            var oldest = oldSlots == 0 ? new List<MatchVideoDiscoveryQueueItem>()
                : await due.Where(q => q.EndUtc < monthAgo).OrderBy(q => q.NextAttemptUtc).ThenByDescending(q => q.EndUtc)
                    .Take(oldSlots).ToListAsync(ct).ConfigureAwait(false);

            // Yeni şeridin alacağı ilk (max - eskiPayı) satır; eski şerit bunlarda olmayanlarla dolar; boş kalan yer yenilerle.
            var head = newest.Take(max - oldest.Count).ToList();
            var result = new List<MatchVideoDiscoveryQueueItem>(head);
            foreach (var o in oldest) if (result.All(r => r.MatchId != o.MatchId)) result.Add(o);
            foreach (var x in newest) if (result.Count < max && result.All(r => r.MatchId != x.MatchId)) result.Add(x);
            return result.Take(max).ToList();
        }

        /// <summary>Atomik rezervasyon — satırı yalnız bir işçi alır.</summary>
        public async Task<bool> TryClaimAsync(int matchId, DateTime nowUtc, CancellationToken ct = default)
        {
            var until = nowUtc + LockDuration;
            if (_db.Database.IsRelational())
            {
                var n = await _db.MatchVideoDiscoveryQueue
                    .Where(q => q.MatchId == matchId && (q.LockedUntilUtc == null || q.LockedUntilUtc < nowUtc))
                    .ExecuteUpdateAsync(s => s.SetProperty(q => q.LockedUntilUtc, until).SetProperty(q => q.LockOwner, _owner), ct)
                    .ConfigureAwait(false);
                return n == 1;
            }
            // Test (InMemory) sağlayıcısı: aynı koşul izlenen varlıkla uygulanır.
            var row = await _db.MatchVideoDiscoveryQueue.FirstOrDefaultAsync(q => q.MatchId == matchId, ct).ConfigureAwait(false);
            if (row == null || (row.LockedUntilUtc != null && row.LockedUntilUtc >= nowUtc)) return false;
            row.LockedUntilUtc = until; row.LockOwner = _owner;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// YENİDEN KUYRUĞA ALMA — yanlış/engelli video kapatıldığında maç hemen aranır. Satır yoksa oluşturulur; kilitliyse
        /// kilit bozulmaz (sürmekte olan tur bitince kendi planını yazar), yalnız gerekçe işlenir.
        /// </summary>
        public static async Task RequeueAsync(FormaxDbContext db, int matchId, string reason, DateTime nowUtc, CancellationToken ct = default)
        {
            var row = await db.MatchVideoDiscoveryQueue.FirstOrDefaultAsync(q => q.MatchId == matchId, ct).ConfigureAwait(false);
            if (row == null)
            {
                var m = await db.Matches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == matchId, ct).ConfigureAwait(false);
                if (m == null || string.IsNullOrWhiteSpace(m.ExternalMatchId)) return;
                var end = MatchVideoIdentityValidator.EndOf(m.MatchDate);
                row = NewItem(m, end, Bucket(end, nowUtc), nowUtc, VideoDiscoveryStates.NotAvailableYet, nowUtc);
                db.MatchVideoDiscoveryQueue.Add(row);
            }
            var videos = await db.MatchVideos.AsNoTracking().Where(v => v.MatchId == matchId)
                .Select(v => new { v.VideoType, v.CanPlayInApp, v.DiscoveryProvenance, v.EvidencePageUrl, v.VerificationStatus }).ToListAsync(ct).ConfigureAwait(false);
            var full = videos.Any(v => v.CanPlayInApp && v.DiscoveryProvenance == MatchVideoRules.OfficialWebProvenance && IsFull(v.VideoType));
            var goals = videos.Any(v => v.CanPlayInApp && v.DiscoveryProvenance == MatchVideoRules.OfficialWebProvenance && IsGoal(v.VideoType));
            var blocked = videos.Any(v => v.VerificationStatus is MatchVideoVerificationStatuses.SourceBlocked or MatchVideoVerificationStatuses.EmbedBlocked);
            row.State = VideoDiscoveryStates.Resolve(full, goals, blocked && !full && !goals, false, Math.Max(row.AttemptCount, VideoDiscoverySchedule.SearchingAttempts));
            if (!VideoDiscoveryStates.StopsRetrying(row.State) && (row.NextAttemptUtc == null || row.NextAttemptUtc > nowUtc))
                row.NextAttemptUtc = nowUtc;
            if (row.State == VideoDiscoveryStates.FullHighlightsAvailable) row.NextAttemptUtc = null;
            row.RequeueReason = reason.Length <= 64 ? reason : reason[..64];
            row.RequeuedAtUtc = nowUtc;
            row.LastOutcome = row.State;
        }

        /// <summary>Resmî sonuç yazıldığında: maç kuyrukta yoksa bitişine göre planlı "aranıyor" satırı açılır.</summary>
        public static async Task EnsureQueuedAfterResultAsync(FormaxDbContext db, Match match, DateTime nowUtc, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(match.ExternalMatchId) || !LockedCompetitions.All.Contains(match.LeagueId)) return;
            if (await db.MatchVideoDiscoveryQueue.AnyAsync(q => q.MatchId == match.Id, ct).ConfigureAwait(false)) return;
            var end = MatchVideoIdentityValidator.EndOf(match.MatchDate);
            var item = NewItem(match, end, Bucket(end, nowUtc), nowUtc, VideoDiscoveryStates.Searching, VideoDiscoverySchedule.NextAttempt(end, 0, null));
            item.RequeueReason = "OfficialResult";
            item.RequeuedAtUtc = nowUtc;
            db.MatchVideoDiscoveryQueue.Add(item);
        }

        /// <summary>Tek maç için bir keşif turu: aday → kimlik kapısı → kayıt → defter → yeni durum.</summary>
        public async Task<string> ProcessAsync(int matchId, DateTime nowUtc, CancellationToken ct = default)
        {
            var item = await _db.MatchVideoDiscoveryQueue.FirstAsync(q => q.MatchId == matchId, ct).ConfigureAwait(false);
            var attemptNo = item.AttemptCount + 1;
            var started = DateTime.UtcNow;
            var technicalFailure = false;
            string outcome;
            string? error = null;
            var rows = new List<MatchVideoDiscoveryAttempt>();

            try
            {
                var identity = await _registrar.BuildIdentityAsync(matchId, ct).ConfigureAwait(false);
                if (identity == null)
                {
                    technicalFailure = true; error = "maç/fikstür kimliği kurulamadı";
                }
                else
                {
                    var found = await _provider.DiscoverAsync(identity, ct).ConfigureAwait(false);
                    if (_provider is IVideoDiscoveryDiagnostics diag && !diag.LastRunCompleted)
                    {
                        technicalFailure = true;
                        error = string.Join(" | ", diag.LastOutcomes.Select(o => o.Provider + "=" + o.Note));
                    }

                    var sources = _catalog.Current();
                    foreach (var c in found)
                    {
                        var r = await _registrar.RegisterAsync(matchId, c, ct).ConfigureAwait(false);
                        _requests?.RecordVerdict(new Telemetry.VideoDiscoveryRequestLog.VerdictEntry(DateTime.UtcNow, c.ProviderName, matchId,
                            identity.ExternalFixtureId, c.SourceIdentifier, c.ExternalVideoId, c.Title, r.Stored, r.Status, r.Reason));

                        // Defter: kabul edilen ya da iki takımdan birini anan adaylar (ilgisiz akış girişleri sayılır, yazılmaz).
                        var folded = MatchVideoIdentityValidator.Fold(c.Title);
                        var relevant = r.Stored || r.Status == "Duplicate"
                                       || TeamNameAliases.Mentions(folded, identity.HomeTeamName)
                                       || TeamNameAliases.Mentions(folded, identity.AwayTeamName);
                        if (!relevant) continue;
                        var src = MatchVideoIdentityValidator.ResolveSource(c, sources);
                        var verdict = MatchVideoIdentityValidator.Validate(c, identity, sources);
                        rows.Add(Row(item, attemptNo, nowUtc, "Candidate", src?.Key, SourceKind(src, identity),
                            c.EvidencePageUrl != null && Uri.TryCreate(c.EvidencePageUrl, UriKind.Absolute, out var ev) ? ev.Host : c.SourceIdentifier,
                            c.EvidencePageUrl != null ? "web:" + c.EvidencePageUrl : "feed:" + c.SourceIdentifier,
                            null, null, c.SourcePageUrl, c.Title, c.PublishedUtc, c.DurationSeconds, verdict.VideoType,
                            r.Stored, r.Status, r.Stored || r.Status == "Duplicate" ? r.Reason : null,
                            verdict.Accepted ? verdict.Reason + (c.DatePrecision != null ? "; tarih=" + c.DatePrecision : "") : null,
                            verdict.Accepted ? null : r.Reason));
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                technicalFailure = true; error = ex.GetType().Name + ": " + ex.Message;
                _log.LogWarning(ex, "[VIDEO-QUEUE] {MatchId} taraması hata verdi", matchId);
            }

            // Kaynak istekleri (arama ifadesi + HTTP sonucu) bu tur için süreç kaydından deftere.
            foreach (var req in (_requests?.RequestSnapshot() ?? Array.Empty<Telemetry.VideoDiscoveryRequestLog.RequestEntry>())
                         .Where(q => q.MatchId == matchId && q.AtUtc >= started))
                rows.Add(Row(item, attemptNo, nowUtc, "SourceRequest", null, req.Provider, req.Host,
                    Trim(req.Path + (string.IsNullOrEmpty(req.Query) ? "" : "?" + req.Query), 300),
                    int.TryParse(req.Result, out var code) ? code : null, int.TryParse(req.Result, out _) ? null : req.Result,
                    null, null, null, null, null, false, null, null, $"aday={req.CandidateCount}", null));

            var videos = await _db.MatchVideos.AsNoTracking().Where(v => v.MatchId == matchId)
                .Select(v => new { v.VideoType, v.CanPlayInApp, v.VerificationStatus, v.DiscoveryProvenance }).ToListAsync(ct).ConfigureAwait(false);
            var full = videos.Any(v => v.CanPlayInApp && v.DiscoveryProvenance == MatchVideoRules.OfficialWebProvenance && IsFull(v.VideoType));
            var goals = videos.Any(v => v.CanPlayInApp && v.DiscoveryProvenance == MatchVideoRules.OfficialWebProvenance && IsGoal(v.VideoType));
            var blocked = videos.Any(v => !v.CanPlayInApp && v.VerificationStatus is MatchVideoVerificationStatuses.EmbedBlocked or MatchVideoVerificationStatuses.SourceBlocked);

            if (!technicalFailure) item.AttemptCount = attemptNo;
            item.State = VideoDiscoveryStates.Resolve(full, goals, blocked, technicalFailure, item.AttemptCount);
            item.NextAttemptUtc = VideoDiscoveryStates.StopsRetrying(item.State)
                ? null
                : technicalFailure ? nowUtc + VideoDiscoverySchedule.FailedRetry
                : VideoDiscoverySchedule.NextAttempt(item.EndUtc, item.AttemptCount, nowUtc);
            item.LastAttemptUtc = nowUtc;
            item.LastError = error == null ? null : Trim(error, 400);
            outcome = item.State;
            item.LastOutcome = outcome;
            if ((full || goals) && item.FoundAtUtc == null) item.FoundAtUtc = nowUtc;
            item.LockedUntilUtc = null; item.LockOwner = null;

            foreach (var r in rows) r.NextAttemptUtc = item.NextAttemptUtc;
            _db.MatchVideoDiscoveryAttempts.AddRange(rows);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return $"{matchId}={outcome}";
        }

        public static bool IsFull(string? type) => type is MatchVideoTypes.MatchHighlights or MatchVideoTypes.ExtendedHighlights;
        public static bool IsGoal(string? type) => type is MatchVideoTypes.Goal or "Penalty";

        /// <summary>Yaş kovası (Europe/Istanbul gün sınırı): Today | Yesterday | Last7Days | Last30Days | CurrentSeason | OlderSeason.</summary>
        public static string Bucket(DateTime endUtc, DateTime nowUtc)
        {
            var today = IstanbulDay(nowUtc);
            var day = IstanbulDay(endUtc);
            if (day == today) return "Today";
            if (day == today.AddDays(-1)) return "Yesterday";
            if (endUtc >= nowUtc.AddDays(-7)) return "Last7Days";
            if (endUtc >= nowUtc.AddDays(-30)) return "Last30Days";
            var seasonStart = new DateTime(nowUtc.Month >= 7 ? nowUtc.Year : nowUtc.Year - 1, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            return endUtc >= seasonStart ? "CurrentSeason" : "OlderSeason";
        }

        private static string SourceKind(OfficialVideoSource? s, VideoFixtureIdentity f)
        {
            if (s == null) return "Unknown";
            return s.Tier switch
            {
                OfficialVideoSourceTiers.Federation => "Federation",
                OfficialVideoSourceTiers.League => "League",
                OfficialVideoSourceTiers.Broadcaster => "Broadcaster",
                OfficialVideoSourceTiers.Club => s.TeamId == f.AwayTeamId ? "AwayClub" : "HomeClub",
                _ => s.Platform == "Web" ? "Web" : "Other"
            };
        }

        private static MatchVideoDiscoveryQueueItem NewItem(Match m, DateTime end, string reason, DateTime nowUtc, string state, DateTime? next)
            => new()
            {
                MatchId = m.Id, ExternalFixtureId = m.ExternalMatchId!, HomeTeamId = m.HomeTeamId, AwayTeamId = m.AwayTeamId,
                LeagueId = m.LeagueId, Season = m.MatchDate.Month >= 7 ? m.MatchDate.Year : m.MatchDate.Year - 1,
                KickoffUtc = m.MatchDate, EndUtc = end, State = state, NextAttemptUtc = next, EnqueueReason = reason,
                EnqueuedAtUtc = nowUtc, FoundAtUtc = VideoDiscoveryStates.StopsRetrying(state) ? nowUtc : null
            };

        private static MatchVideoDiscoveryAttempt Row(MatchVideoDiscoveryQueueItem q, int attemptNo, DateTime at, string kind, string? sourceKey,
            string? sourceKind, string? channelOrDomain, string? search, int? http, string? errorType, string? url, string? title,
            DateTime? published, int? duration, string? videoType, bool accepted, string? status, string? embed, string? evidence, string? rejection)
            => new()
            {
                MatchId = q.MatchId, ExternalFixtureId = q.ExternalFixtureId, HomeTeamId = q.HomeTeamId, AwayTeamId = q.AwayTeamId,
                LeagueId = q.LeagueId, Season = q.Season, KickoffUtc = q.KickoffUtc, EndUtc = q.EndUtc, AttemptNo = attemptNo,
                AttemptedAtUtc = at, RowKind = kind, SourceKey = Cut(sourceKey, 80), SourceKind = Cut(sourceKind, 24),
                SourceChannelOrDomain = Cut(channelOrDomain, 200), SearchExpression = Cut(search, 300), HttpStatus = http,
                ErrorType = Cut(errorType, 64), CandidateUrl = Cut(url, 600), CandidateTitle = Cut(title, 300),
                CandidatePublishedUtc = published, DurationSeconds = duration, VideoType = Cut(videoType, 32), Accepted = accepted,
                VerificationStatus = Cut(status, 32), EmbedResult = Cut(embed, 200), Evidence = Cut(evidence, 600), RejectionReason = Cut(rejection, 300)
            };

        private static string? Cut(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
        private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];

        private static readonly TimeZoneInfo Istanbul = ResolveIstanbul();
        private static TimeZoneInfo ResolveIstanbul()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
        }
        public static DateOnly IstanbulDay(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Istanbul));
    }
}
