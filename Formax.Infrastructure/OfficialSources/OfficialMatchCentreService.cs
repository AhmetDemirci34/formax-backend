using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Domain.Enums;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.OfficialSources
{
    public sealed record MatchCentreMatchOutcome(int MatchId, string SourceKey, string Outcome, string? Detail);

    public sealed record MatchCentreRoundReport(
        string RoundKey,
        IReadOnlyList<string> SourcesRead,
        IReadOnlyList<MatchCentreMatchOutcome> Matches,
        int DevelopmentsRecorded,
        int NotificationsCreated);

    /// <summary>
    /// RESMÎ MAÇ MERKEZİ TURU — kaynak başına maç listesi TUR BAŞINA BİR KEZ okunur; listedeki bütün
    /// FORMAX maçları aynı cevapla eşleştirilir. Bu turda:
    ///  • kritik gelişmeler (erteleme/iptal/askı/saat/stat) doğrulanır, kalıcı yazılır, ANCAK işlem
    ///    başarıyla bittikten sonra aktif takipçilere bildirilir;
    ///  • resmî başlama saati maça yazılır (lisanslı sağlayıcının takvim senkronu geri almaz).
    /// API-Football ÇAĞRILMAZ. Kullanıcı istek yolu bu sınıfı kullanmaz.
    /// </summary>
    public sealed class OfficialMatchCentreService
    {
        public const string CriticalTitle = "Maçla ilgili kritik gelişme";

        /// <summary>Normal şartlarda maç başına en fazla bu kadar (Critical hariç) kritik gelişme bildirimi.</summary>
        public const int MaxNonCriticalNotificationsPerMatch = 3;

        private readonly FormaxDbContext _db;
        private readonly IReadOnlyList<IOfficialCompetitionSource> _sources;
        private readonly IMatchNotificationDispatcher _notifier;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialMatchCentreService> _log;

        public OfficialMatchCentreService(
            FormaxDbContext db, IEnumerable<IOfficialCompetitionSource> sources, IMatchNotificationDispatcher notifier,
            IConfiguration config, ILogger<OfficialMatchCentreService> log)
        {
            _db = db;
            _sources = sources.ToList();
            _notifier = notifier;
            _config = config;
            _log = log;
        }

        public async Task<MatchCentreRoundReport> RunRoundAsync(DateTime utcNow, CancellationToken ct = default)
        {
            var roundKey = $"centre:{utcNow:yyyyMMddHHmm}";
            var allow = CoveragePolicy.LeagueAllowList(_config);
            var from = utcNow.AddDays(-1);
            var to = utcNow.AddDays(3);

            var matches = (await _db.Matches
                    .Include(m => m.HomeTeam).Include(m => m.AwayTeam)
                    .Where(m => m.MatchDate >= from && m.MatchDate <= to)
                    .ToListAsync(ct))
                .Where(m => CoveragePolicy.Allows(allow, m.LeagueId))
                .ToList();

            var outcomes = new List<MatchCentreMatchOutcome>();
            var sourcesRead = new List<string>();
            var recorded = 0;
            var notified = 0;

            foreach (var source in _sources)
            {
                var descriptor = OfficialSourceRegistry.ByKey(source.SourceKey);
                if (descriptor?.Status != OfficialSourceStatuses.Verified || !source.Purposes.Contains(OfficialPurposes.Schedule))
                    continue;
                var group = matches.Where(m => descriptor.LeagueIds.Contains(m.LeagueId)).ToList();
                if (group.Count == 0) continue;

                var feed = await source.ReadMatchesAsync(new OfficialRoundContext(roundKey, utcNow, OfficialPurposes.Critical), ct);
                sourcesRead.Add($"{source.SourceKey}:{feed.Outcome}");
                if (!feed.Ok)
                {
                    outcomes.AddRange(group.Select(m => new MatchCentreMatchOutcome(m.Id, source.SourceKey, "FetchFailed", feed.Detail)));
                    continue;
                }

                foreach (var match in group)
                {
                    ct.ThrowIfCancellationRequested();
                    var (outcome, rec, notes) = await ProcessAsync(match, source.SourceKey, feed.Value!, utcNow, ct);
                    outcomes.Add(outcome);
                    recorded += rec;
                    notified += notes;
                }
            }

            notified += await CompletePendingAsync(utcNow, ct);

            if (recorded > 0 || notified > 0)
                _log.LogInformation("[MATCH CENTRE] {Round}: {Recorded} kritik gelişme, {Notified} bildirim", roundKey, recorded, notified);
            return new MatchCentreRoundReport(roundKey, sourcesRead, outcomes, recorded, notified);
        }

        private async Task<(MatchCentreMatchOutcome Outcome, int Recorded, int Notified)> ProcessAsync(
            Match match, string sourceKey, IReadOnlyList<OfficialMatchRecord> feed, DateTime utcNow, CancellationToken ct)
        {
            var home = match.HomeTeam?.Name ?? string.Empty;
            var away = match.AwayTeam?.Name ?? string.Empty;
            var link = await _db.OfficialMatchLinks.FirstOrDefaultAsync(l => l.MatchId == match.Id && l.SourceKey == sourceKey, ct);

            // Bağlantı varsa kaynak kimliğiyle bulunur (ertelenip tarihi kaymış maç da yakalanır) ve
            // ev/deplasman yönü YENİDEN sınanır; yoksa tarih penceresiyle kimlik çözülür.
            OfficialMatchRecord? record = null;
            if (link != null)
            {
                record = feed.FirstOrDefault(r => r.OfficialMatchId == link.OfficialMatchId);
                if (record != null && !(OfficialTeamNameMatcher.SameTeam(record.HomeName, home)
                                        && OfficialTeamNameMatcher.SameTeam(record.AwayName, away)))
                    return (new(match.Id, sourceKey, "IdentityRejected", "LinkOrientationMismatch"), 0, 0);
            }
            if (record == null)
            {
                var decision = OfficialMatchIdentityResolver.Resolve(
                    new FormaxMatchIdentity(match.Id, match.LeagueId, home, away, match.MatchDate), feed);
                if (!decision.Accepted) return (new(match.Id, sourceKey, "IdentityRejected", decision.Reason), 0, 0);
                record = decision.Record!;
            }

            var previous = link == null ? null : new OfficialObservedState(link.OfficialKickoffUtc, link.OfficialVenue, link.OfficialStatus);
            var observations = CriticalDevelopmentDetector.Detect(match.Id, home, away, match.MatchDate, previous, record, utcNow, match.Status);

            // ── TEK İŞLEM: gelişmeler + bağlantı durumu + resmî saat ────────────────
            var fresh = new List<MatchCriticalDevelopment>();
            foreach (var o in observations)
            {
                if (await _db.MatchCriticalDevelopments.AnyAsync(d => d.MatchId == match.Id && d.EvidenceHash == o.EvidenceHash, ct))
                    continue; // aynı gelişme ikinci kez yazılmaz
                var row = new MatchCriticalDevelopment
                {
                    MatchId = match.Id,
                    SourceKey = sourceKey,
                    OfficialUrl = record.OfficialUrl,
                    SourcePublishedAtUtc = null,
                    DevelopmentType = o.Type,
                    Severity = o.Severity,
                    VerificationStatus = "Verified",
                    EvidenceHash = o.EvidenceHash,
                    PreviousValue = o.PreviousValue,
                    NewValue = o.NewValue,
                    SummaryTr = o.SummaryTr,
                    DiscoveredAtUtc = utcNow
                };
                _db.MatchCriticalDevelopments.Add(row);
                fresh.Add(row);
            }

            if (link == null)
            {
                if (!await _db.OfficialMatchLinks.AnyAsync(l => l.SourceKey == sourceKey && l.OfficialMatchId == record.OfficialMatchId, ct))
                {
                    link = new OfficialMatchLink
                    {
                        MatchId = match.Id, SourceKey = sourceKey, OfficialMatchId = record.OfficialMatchId,
                        OfficialUrl = record.OfficialUrl, OfficialHomeName = record.HomeName, OfficialAwayName = record.AwayName,
                        LinkedAtUtc = utcNow
                    };
                    _db.OfficialMatchLinks.Add(link);
                }
            }
            if (link != null)
            {
                var unknownTime = record.Extra?.GetValueOrDefault("kickoffUnknown") == "true";
                if (record.KickoffUtc.HasValue && !unknownTime) link.OfficialKickoffUtc = record.KickoffUtc;
                if (!string.IsNullOrWhiteSpace(record.Venue)) link.OfficialVenue = record.Venue;
                link.OfficialStatus = record.Status;
                link.VerifiedAtUtc = utcNow;
            }

            var tracked = await _db.Matches.FirstAsync(m => m.Id == match.Id, ct);
            if (record.Status == OfficialMatchStatuses.Scheduled && record.KickoffUtc is DateTime k
                && record.Extra?.GetValueOrDefault("kickoffUnknown") != "true"
                && (k - tracked.MatchDate).Duration() >= TimeSpan.FromMinutes(1))
            {
                tracked.MatchDate = k;
                tracked.KickoffPrecision = KickoffPrecisions.Confirmed;
                tracked.ScheduleVerifiedAtUtc = utcNow;
                tracked.ScheduleSource = OfficialLineupCollector.ProviderPrefix + sourceKey;
            }
            else if (record.Status == OfficialMatchStatuses.Scheduled && record.KickoffUtc.HasValue
                     && record.Extra?.GetValueOrDefault("kickoffUnknown") != "true")
            {
                tracked.ScheduleSource ??= OfficialLineupCollector.ProviderPrefix + sourceKey;
                tracked.ScheduleVerifiedAtUtc = utcNow;
            }

            await _db.SaveChangesAsync(ct);

            // ── Bildirim — YALNIZ işlem başarıyla yazıldıktan sonra ─────────────────
            var notified = 0;
            foreach (var row in fresh)
                notified += await NotifyAsync(match, row, utcNow, ct);

            return (new(match.Id, sourceKey, fresh.Count > 0 ? "DevelopmentRecorded" : "NoChange",
                fresh.Count > 0 ? string.Join(",", fresh.Select(f => f.DevelopmentType)) : null), fresh.Count, notified);
        }

        /// <summary>
        /// Gelişme yazıldı ama süreç dağıtımdan önce durduysa (NotifiedAtUtc ve not boş) son 24 saatin
        /// kayıtları için dağıtım tamamlanır; kullanıcı başına tekillik DB anahtarındadır.
        /// </summary>
        private async Task<int> CompletePendingAsync(DateTime utcNow, CancellationToken ct)
        {
            var since = utcNow.AddHours(-24);
            var pending = await _db.MatchCriticalDevelopments
                .Where(d => d.NotifiedAtUtc == null && d.NotificationNote == null && d.DiscoveredAtUtc >= since)
                .ToListAsync(ct);
            var created = 0;
            foreach (var row in pending)
            {
                var match = await _db.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).FirstOrDefaultAsync(m => m.Id == row.MatchId, ct);
                if (match != null) created += await NotifyAsync(match, row, utcNow, ct);
            }
            return created;
        }

        private async Task<int> NotifyAsync(Match match, MatchCriticalDevelopment row, DateTime utcNow, CancellationToken ct)
        {
            if (row.Severity != CriticalSeverities.Critical)
            {
                // Sınır, gerçekten kullanıcıya ulaşmış (en az bir bildirim yazılmış) gelişmeleri sayar.
                var sent = await _db.MatchCriticalDevelopments.CountAsync(d => d.MatchId == match.Id
                    && d.Severity != CriticalSeverities.Critical && d.NotifiedAtUtc != null
                    && d.NotificationNote != null && !d.NotificationNote.Contains("created=0;"), ct);
                if (sent >= MaxNonCriticalNotificationsPerMatch)
                {
                    row.NotificationNote = "NotificationCapReached";
                    await _db.SaveChangesAsync(ct);
                    return 0;
                }
            }

            var result = await _notifier.DispatchAsync(new MatchNotificationRequest(
                match.Id,
                MatchNotificationTypes.CriticalUpdate,
                CriticalTitle,
                row.SummaryTr,
                NotificationEventType.News,
                userId => MatchNotificationTypes.CriticalKey(match.Id, row.EvidenceHash, userId),
                utcNow,
                match.LeagueId), ct);

            row.NotifiedAtUtc = utcNow;
            row.NotificationNote = $"followers={result.ActiveFollowers};created={result.Created};dup={result.SkippedDuplicate};pref={result.SkippedByPreference}";
            await _db.SaveChangesAsync(ct);
            return result.Created;
        }
    }
}
