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
        int NotificationsCreated,
        int ResultsApplied = 0);

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
        private readonly IMatchLiveStatsRepository? _liveStats;
        private readonly ILeagueStandingsService? _standings;

        public OfficialMatchCentreService(
            FormaxDbContext db, IEnumerable<IOfficialCompetitionSource> sources, IMatchNotificationDispatcher notifier,
            IConfiguration config, ILogger<OfficialMatchCentreService> log,
            IMatchLiveStatsRepository? liveStats = null, ILeagueStandingsService? standings = null)
        {
            _db = db;
            _sources = sources.ToList();
            _notifier = notifier;
            _config = config;
            _log = log;
            _liveStats = liveStats;
            _standings = standings;
        }

        public async Task<MatchCentreRoundReport> RunRoundAsync(DateTime utcNow, CancellationToken ct = default)
        {
            var roundKey = $"centre:{utcNow:yyyyMMddHHmm}";
            var allow = CoveragePolicy.LeagueAllowList(_config);
            // Geriye 3 gün: sonucu kesinleşmemiş (NotStarted/Live kalmış) yakın maçlar da resmî listeyle kapanır.
            var from = utcNow.AddDays(-3);
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
            var results = 0;

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
                    var (outcome, rec, notes, record) = await ProcessAsync(match, source.SourceKey, feed.Value!, utcNow, ct);
                    outcomes.Add(outcome);
                    recorded += rec;
                    notified += notes;
                    if (record != null)
                    {
                        var r = await ApplyStatusAndResultAsync(match.Id, source, record, roundKey, utcNow, ct);
                        if (r != null) outcomes.Add(r);
                        if (r?.Outcome == ResultOutcomes.Applied) results++;
                    }
                }
            }

            notified += await CompletePendingAsync(utcNow, ct);

            if (recorded > 0 || notified > 0 || results > 0)
                _log.LogInformation("[MATCH CENTRE] {Round}: {Recorded} kritik gelişme, {Notified} bildirim, {Results} resmî sonuç",
                    roundKey, recorded, notified, results);
            return new MatchCentreRoundReport(roundKey, sourcesRead, outcomes, recorded, notified, results);
        }

        private async Task<(MatchCentreMatchOutcome Outcome, int Recorded, int Notified, OfficialMatchRecord? Record)> ProcessAsync(
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
                    return (new(match.Id, sourceKey, "IdentityRejected", "LinkOrientationMismatch"), 0, 0, null);
            }
            if (record == null)
            {
                var decision = OfficialMatchIdentityResolver.Resolve(
                    new FormaxMatchIdentity(match.Id, match.LeagueId, home, away, match.MatchDate), feed);
                if (!decision.Accepted) return (new(match.Id, sourceKey, "IdentityRejected", decision.Reason), 0, 0, null);
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
                fresh.Count > 0 ? string.Join(",", fresh.Select(f => f.DevelopmentType)) : null), fresh.Count, notified, record);
        }

        public static class ResultOutcomes
        {
            public const string Applied = "ResultApplied";
            public const string Unchanged = "ResultUnchanged";
            public const string VerificationPending = "VerificationPending";
            public const string ConfirmationFetchFailed = "ResultConfirmationFetchFailed";
            public const string StatusApplied = "StatusApplied";
        }

        /// <summary>
        /// DURUM VE SONUÇ — API-Football DEĞİL, yalnız resmî kaynağın kaydından.
        ///  • Finished + iki skor: kaynak "teyit" sunuyorsa (TFF maç sayfası) skor karşılaştırılır;
        ///    uyuşmazsa ya da sayfada skor yoksa sonuç KESİNLEŞTİRİLMEZ (VerificationPending).
        ///  • Live: yalnız başlamamış görünen maç "Live" olur.
        ///  • Postponed / Cancelled: bitmemiş maça yazılır.
        /// Kesin sonuç geri alınmaz; skor 0-0 uydurulmaz (skor yoksa yazım yok).
        /// </summary>
        private async Task<MatchCentreMatchOutcome?> ApplyStatusAndResultAsync(
            int matchId, IOfficialCompetitionSource source, OfficialMatchRecord record, string roundKey, DateTime utcNow, CancellationToken ct)
        {
            var descriptor = OfficialSourceRegistry.ByKey(source.SourceKey);
            if (descriptor == null || !descriptor.Capabilities.Contains(OfficialPurposes.Result)) return null;
            var tracked = await _db.Matches.FirstAsync(m => m.Id == matchId, ct);
            var isFinished = string.Equals(tracked.Status, MatchStatuses.Finished, StringComparison.OrdinalIgnoreCase);
            var official = OfficialLineupCollector.ProviderPrefix + source.SourceKey;

            if (record.Status == OfficialMatchStatuses.Finished && record.HomeScore is int hs && record.AwayScore is int aws)
            {
                if (isFinished && tracked.HomeScore == hs && tracked.AwayScore == aws && tracked.ResultSource == official)
                    return new(matchId, source.SourceKey, ResultOutcomes.Unchanged, hs + "-" + aws);

                if (source is IOfficialResultConfirmation confirmation)
                {
                    var c = await confirmation.ConfirmScoreAsync(record,
                        new OfficialRoundContext(roundKey, utcNow, OfficialPurposes.Result, matchId), ct);
                    if (!c.Ok) return new(matchId, source.SourceKey, ResultOutcomes.ConfirmationFetchFailed, c.Detail);
                    var confirmed = c.Value;
                    if (confirmed == null || confirmed.Value.Home != hs || confirmed.Value.Away != aws)
                    {
                        tracked.ResultVerificationStatus = "VerificationPending";
                        await _db.SaveChangesAsync(ct);
                        var pageScore = confirmed == null ? "skor yok" : confirmed.Value.Home + "-" + confirmed.Value.Away;
                        return new(matchId, source.SourceKey, ResultOutcomes.VerificationPending,
                            "liste " + hs + "-" + aws + " / maç sayfası " + pageScore);
                    }
                }

                tracked.Status = MatchStatuses.Finished;
                tracked.HomeScore = hs;
                tracked.AwayScore = aws;
                if (record.HalfTimeHome.HasValue && record.HalfTimeAway.HasValue)
                {
                    tracked.HalfTimeHomeScore = record.HalfTimeHome;
                    tracked.HalfTimeAwayScore = record.HalfTimeAway;
                }
                tracked.ResultUpdatedAtUtc = utcNow;
                tracked.ResultSource = official;
                tracked.ResultVerificationStatus = "Verified";
                await _db.SaveChangesAsync(ct);

                // Maç başlığı/karar okuması skoru MatchLiveStats'tan da okur — iki kayıt aynı hizada tutulur.
                if (_liveStats != null)
                {
                    try
                    {
                        await _liveStats.UpsertAsync(new MatchLiveStats
                        {
                            MatchId = matchId, HomeScore = hs, AwayScore = aws, Minute = 90, Phase = "FT", UpdatedAt = utcNow
                        }, ct);
                        await _liveStats.SaveChangesAsync(ct);
                    }
                    catch (Exception ex) { _log.LogWarning(ex, "[MATCH CENTRE] {MatchId} skor aynası yazılamadı", matchId); }
                }
                if (_standings != null)
                {
                    try { await _standings.RefreshForSettledMatchAsync(tracked.LeagueId, tracked.MatchDate, ct); }
                    catch (Exception ex) { _log.LogWarning(ex, "[MATCH CENTRE] {MatchId} puan durumu yenilenemedi", matchId); }
                }

                _log.LogInformation("[MATCH CENTRE] {MatchId} resmî sonuç yazıldı: {Home}-{Away} ({Source})", matchId, hs, aws, official);
                return new(matchId, source.SourceKey, ResultOutcomes.Applied, hs + "-" + aws);
            }

            if (isFinished) return null; // kesin sonuç geri alınmaz

            string? newStatus = record.Status switch
            {
                OfficialMatchStatuses.Live when tracked.Status == MatchStatuses.NotStarted => MatchStatuses.Live,
                OfficialMatchStatuses.Postponed => "Postponed",
                OfficialMatchStatuses.Cancelled => "Cancelled",
                _ => null
            };
            if (newStatus == null || string.Equals(tracked.Status, newStatus, StringComparison.OrdinalIgnoreCase)) return null;
            tracked.Status = newStatus;
            await _db.SaveChangesAsync(ct);
            return new(matchId, source.SourceKey, ResultOutcomes.StatusApplied, newStatus);
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
