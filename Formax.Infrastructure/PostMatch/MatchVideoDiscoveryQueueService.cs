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
    public sealed record VideoQueueCycleResult(int EnqueuedRecent, int EnqueuedBackfill, int Processed, IReadOnlyList<string> Outcomes);

    /// <summary>
    /// KALICI VİDEO KEŞİF KUYRUĞU — kullanıcı sayfası açmadan çalışır; restart kuyruğu, deneme sayısını ve
    /// sonraki deneme anını kaybetmez (hepsi DB'de).
    ///
    /// Öncelik: bugün biten → dün biten → videosuz eski maçlar (sayfalı backfill; tüm geçmiş belleğe alınmaz).
    /// Aynı maçı iki süreç aynı anda taramaz: satır atomik UPDATE ile kilitlenir.
    /// </summary>
    public sealed class MatchVideoDiscoveryQueueService
    {
        public static readonly TimeSpan RecentWindow = TimeSpan.FromHours(48);
        public static readonly TimeSpan BackfillHorizon = TimeSpan.FromDays(400);
        public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(10);

        private readonly FormaxDbContext _db;
        private readonly IMatchVideoRegistrar _registrar;
        private readonly IOfficialMatchVideoProvider _provider;
        private readonly IOfficialVideoSourceCatalog _catalog;
        private readonly IConfiguration _config;
        private readonly ILogger<MatchVideoDiscoveryQueueService> _log;
        private readonly Telemetry.VideoDiscoveryRequestLog? _requests;
        private readonly string _owner = Environment.MachineName + ":" + Environment.ProcessId;

        public MatchVideoDiscoveryQueueService(FormaxDbContext db, IMatchVideoRegistrar registrar, IOfficialMatchVideoProvider provider,
            IOfficialVideoSourceCatalog catalog, IConfiguration config, ILogger<MatchVideoDiscoveryQueueService> log,
            Telemetry.VideoDiscoveryRequestLog? requests = null)
        {
            _db = db; _registrar = registrar; _provider = provider; _catalog = catalog; _config = config; _log = log; _requests = requests;
        }

        public async Task<VideoQueueCycleResult> RunCycleAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var (recent, backfill) = await EnqueueAsync(nowUtc, ct).ConfigureAwait(false);
            var max = Math.Max(0, _config.GetValue("PostMatch:Video:MaxMatchesPerCycle", 10));
            var outcomes = new List<string>();
            foreach (var item in await DueAsync(nowUtc, max, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                if (!await TryClaimAsync(item.MatchId, nowUtc, ct).ConfigureAwait(false)) continue;
                outcomes.Add(await ProcessAsync(item.MatchId, nowUtc, ct).ConfigureAwait(false));
            }
            if (recent + backfill + outcomes.Count > 0)
                _log.LogInformation("[VIDEO-QUEUE] kuyruğa: yeni={Recent} backfill={Backfill}; işlenen={Processed}: {Outcomes}",
                    recent, backfill, outcomes.Count, string.Join(", ", outcomes));
            return new VideoQueueCycleResult(recent, backfill, outcomes.Count, outcomes);
        }

        /// <summary>Bugün/dün biten maçlar + bir sayfa eski videosuz maç kuyruğa alınır.</summary>
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

            var batch = Math.Max(0, _config.GetValue("PostMatch:Video:BackfillBatch", 50));
            var horizon = nowUtc - BackfillHorizon;
            var backfill = batch == 0 ? new List<Match>() : await _db.Matches.AsNoTracking()
                .Where(m => m.Status == MatchStatuses.Finished && locked.Contains(m.LeagueId)
                            && m.MatchDate < recentFrom && m.MatchDate >= horizon
                            && m.ExternalMatchId != null && m.ExternalMatchId != ""
                            && !_db.MatchVideoDiscoveryQueue.Any(q => q.MatchId == m.Id)
                            && !_db.MatchVideos.Any(v => v.MatchId == m.Id && v.CanPlayInApp))
                .OrderByDescending(m => m.MatchDate).Take(batch)
                .ToListAsync(ct).ConfigureAwait(false);

            var playable = await _db.MatchVideos.AsNoTracking()
                .Where(v => v.CanPlayInApp && recent.Select(r => r.Id).Contains(v.MatchId))
                .Select(v => new { v.MatchId, v.VideoType }).ToListAsync(ct).ConfigureAwait(false);

            var today = IstanbulDay(nowUtc);
            foreach (var m in recent)
            {
                var end = MatchVideoIdentityValidator.EndOf(m.MatchDate);
                var full = playable.Any(p => p.MatchId == m.Id && IsFull(p.VideoType));
                var goals = playable.Any(p => p.MatchId == m.Id && IsGoal(p.VideoType));
                var state = VideoDiscoveryStates.Resolve(full, goals, false, false, 0);
                _db.MatchVideoDiscoveryQueue.Add(NewItem(m, end, IstanbulDay(end) == today ? "Today" : "Yesterday", nowUtc, state,
                    VideoDiscoveryStates.StopsRetrying(state) ? null : VideoDiscoverySchedule.NextAttempt(end, 0, null)));
            }
            foreach (var m in backfill)
            {
                var end = MatchVideoIdentityValidator.EndOf(m.MatchDate);
                // Geçmiş maç: ilk günün planı çoktan geçti; "aranıyor" değil, "henüz bulunamadı" + hemen deneme.
                _db.MatchVideoDiscoveryQueue.Add(NewItem(m, end, "Backfill", nowUtc, VideoDiscoveryStates.NotAvailableYet, nowUtc));
            }
            if (recent.Count + backfill.Count > 0) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return (recent.Count, backfill.Count);
        }

        /// <summary>Zamanı gelen satırlar: önce bugün/dün, sonra en yeni eski maç.</summary>
        public async Task<List<MatchVideoDiscoveryQueueItem>> DueAsync(DateTime nowUtc, int max, CancellationToken ct = default)
            => await _db.MatchVideoDiscoveryQueue.AsNoTracking()
                .Where(q => q.NextAttemptUtc != null && q.NextAttemptUtc <= nowUtc
                            && (q.LockedUntilUtc == null || q.LockedUntilUtc < nowUtc))
                .OrderBy(q => q.EnqueueReason == "Backfill" ? 1 : 0)
                .ThenByDescending(q => q.EndUtc)
                .Take(max).ToListAsync(ct).ConfigureAwait(false);

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
                        var folded = MatchVideoIdentityValidator.Fold(c.Title + " " + c.Description);
                        var relevant = r.Stored || r.Status == "Duplicate"
                                       || TeamNameAliases.Mentions(folded, identity.HomeTeamName)
                                       || TeamNameAliases.Mentions(folded, identity.AwayTeamName);
                        if (!relevant) continue;
                        var src = c.Platform == "YouTube" ? OfficialVideoSources.ByYouTubeChannel(c.SourceIdentifier, sources) : OfficialVideoSources.ByKey(c.SourceIdentifier, sources);
                        var verdict = MatchVideoIdentityValidator.Validate(c, identity, sources);
                        rows.Add(Row(item, attemptNo, nowUtc, "Candidate", src?.Key, SourceKind(src, identity), c.SourceIdentifier,
                            c.Platform == "YouTube" ? "rss:channel_id=" + c.SourceIdentifier : "feed:" + c.SourceIdentifier,
                            null, null, c.SourcePageUrl, c.Title, c.PublishedUtc, c.DurationSeconds, verdict.VideoType,
                            r.Stored, r.Status, r.Stored || r.Status == "Duplicate" ? r.Reason : null,
                            verdict.Accepted ? verdict.Reason : null, verdict.Accepted ? null : r.Reason));
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                technicalFailure = true; error = ex.GetType().Name + ": " + ex.Message;
                _log.LogWarning(ex, "[VIDEO-QUEUE] {MatchId} taraması hata verdi", matchId);
            }

            // Kaynak istekleri (arama ifadesi + HTTP sonucu) bu tur için süreç kaydından defterе.
            foreach (var req in (_requests?.RequestSnapshot() ?? Array.Empty<Telemetry.VideoDiscoveryRequestLog.RequestEntry>())
                         .Where(q => q.MatchId == matchId && q.AtUtc >= started))
                rows.Add(Row(item, attemptNo, nowUtc, "SourceRequest", null, req.Provider, req.Host,
                    Trim(req.Path + (string.IsNullOrEmpty(req.Query) ? "" : "?" + req.Query), 300),
                    int.TryParse(req.Result, out var code) ? code : null, int.TryParse(req.Result, out _) ? null : req.Result,
                    null, null, null, null, null, false, null, null, $"aday={req.CandidateCount}", null));

            var videos = await _db.MatchVideos.AsNoTracking().Where(v => v.MatchId == matchId)
                .Select(v => new { v.VideoType, v.CanPlayInApp, v.VerificationStatus }).ToListAsync(ct).ConfigureAwait(false);
            var full = videos.Any(v => v.CanPlayInApp && IsFull(v.VideoType));
            var goals = videos.Any(v => v.CanPlayInApp && IsGoal(v.VideoType));
            var blocked = videos.Any(v => !v.CanPlayInApp && v.VerificationStatus == MatchVideoVerificationStatuses.EmbedBlocked);

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

        private static string SourceKind(OfficialVideoSource? s, VideoFixtureIdentity f)
        {
            if (s == null) return "Unknown";
            if (s.Platform == "Web") return "Web";
            return s.Tier switch
            {
                OfficialVideoSourceTiers.Federation => "Federation",
                OfficialVideoSourceTiers.League => "League",
                OfficialVideoSourceTiers.Broadcaster => "Broadcaster",
                OfficialVideoSourceTiers.Club => s.TeamId == f.AwayTeamId ? "AwayClub" : "HomeClub",
                _ => "Other"
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
