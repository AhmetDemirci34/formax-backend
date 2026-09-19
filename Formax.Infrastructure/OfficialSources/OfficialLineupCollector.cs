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
    /// <summary>Tek maçın bu turdaki kadro sonucu.</summary>
    public sealed record LineupMatchOutcome(int MatchId, string? SourceKey, string Outcome, string? Detail);

    public static class LineupOutcomes
    {
        public const string Released = "Released";                 // iki taraf doğrulandı ve yazıldı
        public const string PartiallyReleased = "PartiallyReleased"; // yalnız bir taraf doğrulandı
        public const string NotPublished = "NotPublished";           // geçerli boş cevap (başarı DEĞİL)
        public const string Rejected = "Rejected";                   // doğrulamadan geçmedi
        public const string IdentityRejected = "IdentityRejected";   // resmî kaynakta maç kimliği kurulamadı
        public const string FetchFailed = "FetchFailed";             // kontrol SAYILMAZ
        public const string NoOfficialSource = "NoOfficialSource";   // lig için doğrulanmış kaynak yok — istek yok
        public const string AlreadyComplete = "AlreadyComplete";
    }

    public sealed record LineupRoundReport(
        string RoundKey, int WindowMatches, int DueMatches,
        IReadOnlyList<LineupMatchOutcome> Outcomes, int NotificationsCreated);

    /// <summary>
    /// RESMÎ KADRO TOPLAYICI — API-Football KULLANMAZ.
    ///
    /// Tur akışı: kadro penceresindeki (T−60 … kickoff+10) kapsam içi maçlar → slot kuralı
    /// (<see cref="OfficialLineupSchedule"/>) → takip edilen maçlar önce → maçlar kaynaklarına göre
    /// gruplanır → her kaynağın maç listesi TUR BAŞINA BİR KEZ okunur ve bütün maçlar o listeyle
    /// eşleştirilir → kimliği doğrulanan maçın kadrosu okunur → doğrulanır → tek işlemde yazılır →
    /// işlem BAŞARIYLA bittikten sonra takipçilere bildirim.
    ///
    /// Kullanıcı istek yolu bu sınıfı ÇAĞIRMAZ.
    /// </summary>
    public sealed class OfficialLineupCollector
    {
        public const string ProviderPrefix = "official:";

        private readonly FormaxDbContext _db;
        private readonly IReadOnlyDictionary<string, IOfficialCompetitionSource> _sources;
        private readonly IOfficialContentFetcher _fetcher;
        private readonly IMatchNotificationDispatcher _notifier;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialLineupCollector> _log;

        public OfficialLineupCollector(
            FormaxDbContext db,
            IEnumerable<IOfficialCompetitionSource> sources,
            IOfficialContentFetcher fetcher,
            IMatchNotificationDispatcher notifier,
            IConfiguration config,
            ILogger<OfficialLineupCollector> log)
        {
            _db = db;
            _sources = sources.ToDictionary(s => s.SourceKey, StringComparer.Ordinal);
            _fetcher = fetcher;
            _notifier = notifier;
            _config = config;
            _log = log;
        }

        /// <summary>Kadronun resmî kaynaktan geldiğini gösteren sağlayıcı damgası.</summary>
        public static bool IsOfficial(MatchLineup? header)
            => header?.Provider?.StartsWith(ProviderPrefix, StringComparison.Ordinal) == true;

        public static string LineupTitle => "Kadrolar açıklandı";

        public static string LineupMessage(string home, string away)
            => $"{home}–{away} maçının resmî kadroları belli oldu. İlk 11’leri incele.";

        // ════════════════════════════════════════════════════════════════════════
        public async Task<LineupRoundReport> RunRoundAsync(DateTime utcNow, CancellationToken ct = default)
        {
            var roundKey = $"lineup:{utcNow:yyyyMMddHHmm}";
            var allow = CoveragePolicy.LeagueAllowList(_config);
            var from = utcNow - (OfficialLineupSchedule.FinalCheckAfterKickoff + OfficialLineupSchedule.FinalCheckTolerance);
            var to = utcNow + OfficialLineupSchedule.WindowOpen;

            var matches = await _db.Matches
                .Include(m => m.HomeTeam).Include(m => m.AwayTeam)
                .Where(m => m.MatchDate >= from && m.MatchDate <= to)
                .ToListAsync(ct);
            matches = matches.Where(m => CoveragePolicy.Allows(allow, m.LeagueId)).ToList();

            var ids = matches.Select(m => m.Id).ToList();
            var headers = await _db.MatchLineups.AsNoTracking().Where(h => ids.Contains(h.MatchId)).ToListAsync(ct);
            var headerById = headers.ToDictionary(h => h.MatchId);
            var followed = new HashSet<int>(await _db.UserMatchFollows.AsNoTracking()
                .Where(f => f.IsActive && ids.Contains(f.MatchId)).Select(f => f.MatchId).ToListAsync(ct));

            var due = matches
                .Where(m =>
                {
                    headerById.TryGetValue(m.Id, out var h);
                    var complete = h?.HomeLineupsReleased == true && h.AwayLineupsReleased;
                    // Son kontrol yalnız RESMÎ kaynağın gerçek kontrolüdür; eski sağlayıcı izi sayılmaz.
                    var lastReal = IsOfficial(h) ? h!.LastCheckedAtUtc : null;
                    return OfficialLineupSchedule.ShouldCheck(m.MatchDate, utcNow, complete, lastReal);
                })
                // Takip edilen maç önce işlenir; takip edilmeyen de işlenir.
                .OrderByDescending(m => followed.Contains(m.Id))
                .ThenBy(m => m.MatchDate)
                .ToList();

            var outcomes = new List<LineupMatchOutcome>();
            var notified = 0;

            foreach (var group in due.GroupBy(m => PrimarySource(m.LeagueId)))
            {
                if (group.Key == null)
                {
                    outcomes.AddRange(group.Select(m => new LineupMatchOutcome(m.Id, null, LineupOutcomes.NoOfficialSource,
                        "lig için doğrulanmış resmî kadro kaynağı yok — istek üretilmedi")));
                    continue;
                }

                var source = group.Key;
                var round = new OfficialRoundContext(roundKey, utcNow, OfficialPurposes.Lineup);
                var feed = await source.ReadMatchesAsync(round, ct);
                if (!feed.Ok)
                {
                    outcomes.AddRange(group.Select(m => new LineupMatchOutcome(m.Id, source.SourceKey,
                        LineupOutcomes.FetchFailed, feed.Detail)));
                    continue;
                }

                foreach (var match in group)
                {
                    var (outcome, created) = await CollectAsync(match, source, feed.Value!, round with { MatchId = match.Id }, utcNow, ct);
                    outcomes.Add(outcome);
                    notified += created;
                }
            }

            notified += await CompletePendingNotificationsAsync(utcNow, ct);

            if (outcomes.Count > 0)
                _log.LogInformation("[OFFICIAL LINEUP] {Round}: pencere {W}, zamanı gelen {D}: {Outcomes}",
                    roundKey, matches.Count, due.Count,
                    string.Join(", ", outcomes.Select(o => $"{o.MatchId}={o.Outcome}")));

            return new LineupRoundReport(roundKey, matches.Count, due.Count, outcomes, notified);
        }

        /// <summary>
        /// TEK MAÇ — takvim dışında elle doğrulama/geri doldurma (admin ucu). Aynı okuma, doğrulama
        /// ve yazma yolu; ayrı bir kısa yol yoktur. Kadro yoksa hiçbir şey uydurulmaz.
        /// </summary>
        public async Task<LineupMatchOutcome> CollectForMatchAsync(int matchId, DateTime utcNow, CancellationToken ct = default)
        {
            var match = await _db.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam)
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);
            if (match == null) return new LineupMatchOutcome(matchId, null, "MatchNotFound", null);

            var source = PrimarySource(match.LeagueId);
            if (source == null)
                return new LineupMatchOutcome(matchId, null, LineupOutcomes.NoOfficialSource,
                    "lig için doğrulanmış resmî kadro kaynağı yok — istek üretilmedi");

            var round = new OfficialRoundContext($"lineup-manual:{matchId}:{utcNow:yyyyMMddHHmmss}", utcNow,
                OfficialPurposes.Lineup, matchId);
            var feed = await ReadFeedForMatchAsync(source, match, round, ct);
            if (!feed.Ok) return new LineupMatchOutcome(matchId, source.SourceKey, LineupOutcomes.FetchFailed, feed.Detail);

            var (outcome, _) = await CollectAsync(match, source, feed.Value!, round, utcNow, ct);
            await CompletePendingNotificationsAsync(utcNow, ct);
            return outcome;
        }

        /// <summary>
        /// Tek maç için maç listesi: kaynak listeleri "dün…+3 gün" penceresini okur; eski bir maç
        /// elle doğrulanıyorsa pencere o maçın gününe göre kurulur.
        /// </summary>
        private static Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadFeedForMatchAsync(
            IOfficialCompetitionSource source, Match match, OfficialRoundContext round, CancellationToken ct)
            => source.ReadMatchesAsync(round with { UtcNow = match.MatchDate }, ct);

        private IOfficialCompetitionSource? PrimarySource(int leagueId)
            => OfficialSourceRegistry.VerifiedFor(leagueId, OfficialPurposes.Lineup)
                .Select(d => _sources.GetValueOrDefault(d.Key))
                .FirstOrDefault(s => s != null && s.Purposes.Contains(OfficialPurposes.Lineup));

        // ════════════════════════════════════════════════════════════════════════
        private async Task<(LineupMatchOutcome Outcome, int NotificationsCreated)> CollectAsync(
            Match match, IOfficialCompetitionSource source, IReadOnlyList<OfficialMatchRecord> feed,
            OfficialRoundContext round, DateTime utcNow, CancellationToken ct)
        {
            var homeName = match.HomeTeam?.Name ?? string.Empty;
            var awayName = match.AwayTeam?.Name ?? string.Empty;

            var identity = OfficialMatchIdentityResolver.Resolve(
                new FormaxMatchIdentity(match.Id, match.LeagueId, homeName, awayName, match.MatchDate), feed);
            if (!identity.Accepted)
                return (new LineupMatchOutcome(match.Id, source.SourceKey, LineupOutcomes.IdentityRejected, identity.Reason), 0);

            var record = identity.Record!;
            await UpsertLinkAsync(match.Id, record, utcNow, ct);

            var read = await source.ReadLineupAsync(record, round, ct);
            if (!read.Ok)
                return (new LineupMatchOutcome(match.Id, source.SourceKey, LineupOutcomes.FetchFailed,
                    read.Detail ?? read.Outcome), 0);

            if (read.Value == null)
            {
                // GEÇERLİ BOŞ CEVAP: yalnız son gerçek kontrol yazılır; var olan kadro silinmez.
                await MarkCheckedAsync(match.Id, source.SourceKey, utcNow, ct);
                await _fetcher.RecordDecisionAsync(read.Fetch?.LedgerId ?? 0, 0, 0, "NotPublished", ct);
                return (new LineupMatchOutcome(match.Id, source.SourceKey, LineupOutcomes.NotPublished, null), 0);
            }

            var doc = read.Value;
            var verdict = OfficialContentVerificationService.VerifyLineup(doc, homeName, awayName);
            var candidates = (doc.Home != null ? 1 : 0) + (doc.Away != null ? 1 : 0);
            var accepted = (verdict.SourceOfficial && verdict.Home.Accepted ? 1 : 0)
                         + (verdict.SourceOfficial && verdict.Away.Accepted ? 1 : 0);
            var decision = verdict.SourceOfficial
                ? $"home={verdict.Home.Reason};away={verdict.Away.Reason}"
                : $"source={verdict.SourceRejectReason}";
            await _fetcher.RecordDecisionAsync(read.Fetch?.LedgerId ?? 0, candidates, accepted, decision, ct);

            if (!verdict.AnySideAccepted)
            {
                await MarkCheckedAsync(match.Id, source.SourceKey, utcNow, ct);
                return (new LineupMatchOutcome(match.Id, source.SourceKey, LineupOutcomes.Rejected, decision), 0);
            }

            var (wasComplete, isComplete) = await ApplyAsync(match.Id, doc, verdict, utcNow, ct);
            if (read.Fetch?.ContentHash is { } hash) await _fetcher.MarkProcessedAsync(read.Fetch.Url, hash, ct);

            var created = 0;
            if (isComplete && !wasComplete)
                created = await NotifyFollowersAsync(match, utcNow, ct);

            return (new LineupMatchOutcome(match.Id, source.SourceKey,
                isComplete ? LineupOutcomes.Released : LineupOutcomes.PartiallyReleased, decision), created);
        }

        private async Task UpsertLinkAsync(int matchId, OfficialMatchRecord record, DateTime utcNow, CancellationToken ct)
        {
            var link = await _db.OfficialMatchLinks.FirstOrDefaultAsync(
                l => l.MatchId == matchId && l.SourceKey == record.SourceKey, ct);
            if (link == null)
            {
                // Aynı kaynak maçı başka bir FORMAX maçına bağlıysa ikinci bağ kurulmaz.
                if (await _db.OfficialMatchLinks.AnyAsync(
                        l => l.SourceKey == record.SourceKey && l.OfficialMatchId == record.OfficialMatchId, ct))
                    return;
                _db.OfficialMatchLinks.Add(new OfficialMatchLink
                {
                    MatchId = matchId,
                    SourceKey = record.SourceKey,
                    OfficialMatchId = record.OfficialMatchId,
                    OfficialUrl = record.OfficialUrl,
                    OfficialHomeName = record.HomeName,
                    OfficialAwayName = record.AwayName,
                    OfficialKickoffUtc = record.KickoffUtc,
                    LinkedAtUtc = utcNow,
                    VerifiedAtUtc = utcNow
                });
            }
            else
            {
                // Resmî saat/stat/durum geçmişi maç merkezi turunundur (kritik gelişme karşılaştırması
                // önceki gözleme dayanır); kadro turu yalnız bağlantının hâlâ geçerli olduğunu damgalar.
                link.VerifiedAtUtc = utcNow;
            }
            await _db.SaveChangesAsync(ct);
        }

        private async Task MarkCheckedAsync(int matchId, string sourceKey, DateTime utcNow, CancellationToken ct)
        {
            var header = await _db.MatchLineups.FirstOrDefaultAsync(h => h.MatchId == matchId, ct);
            if (header == null)
            {
                _db.MatchLineups.Add(new MatchLineup
                {
                    MatchId = matchId,
                    FetchedAt = utcNow,
                    Provider = ProviderPrefix + sourceKey,
                    SourceKey = sourceKey,
                    LastCheckedAtUtc = utcNow,
                    VerificationStatus = "Pending"
                });
            }
            else
            {
                header.LastCheckedAtUtc = utcNow;
                // Resmî kadrosu olmayan eski başlık artık resmî kontrolün izini taşır.
                if (!header.HomeLineupsReleased && !header.AwayLineupsReleased)
                {
                    header.Provider = ProviderPrefix + sourceKey;
                    header.SourceKey = sourceKey;
                }
            }
            await _db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Doğrulanmış tarafları TEK işlemde yazar. Doğrulanmayan taraf dokunulmadan kalır (başka
        /// bir resmî kaynaktan gelmiş olabilir → iki kulübün açıklaması güvenle birleşir). Aynı
        /// taraf yeniden yazılırken eski oyuncu satırları silinip yenisi eklenir: tekrar yok.
        /// </summary>
        /// <summary>
        /// GEÇMİŞ DOLDURMA YAZIM KAPISI — canlı turla AYNI doğrulama ve yazma yolunu kullanır.
        /// Tek farkı: takipçi bildirimi GÖNDERİLMEZ (maç çoktan oynandı) ve satır geçmiş damgası
        /// taşır. Doğrulama geçilmezse hiçbir şey yazılmaz.
        /// </summary>
        public async Task<LineupMatchOutcome> WriteHistoricalAsync(
            int matchId, string homeName, string awayName, OfficialLineupDocument doc,
            IReadOnlyList<OfficialSubstitution>? substitutions, DateTime utcNow, CancellationToken ct = default)
        {
            var verdict = OfficialContentVerificationService.VerifyLineup(doc, homeName, awayName);
            if (!verdict.AnySideAccepted)
                return new LineupMatchOutcome(matchId, doc.SourceKey, LineupOutcomes.Rejected,
                    verdict.SourceOfficial ? $"home={verdict.Home.Reason};away={verdict.Away.Reason}" : $"source={verdict.SourceRejectReason}");

            var (_, isComplete) = await ApplyAsync(matchId, doc, verdict, utcNow, ct, substitutions, backfill: true);
            return new LineupMatchOutcome(matchId, doc.SourceKey,
                isComplete ? LineupOutcomes.Released : LineupOutcomes.PartiallyReleased, null);
        }

        private async Task<(bool WasComplete, bool IsComplete)> ApplyAsync(
            int matchId, OfficialLineupDocument doc, LineupVerification verdict, DateTime utcNow, CancellationToken ct,
            IReadOnlyList<OfficialSubstitution>? substitutions = null, bool backfill = false)
        {
            await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;

            var header = await _db.MatchLineups.FirstOrDefaultAsync(h => h.MatchId == matchId, ct);
            var isNew = header == null;
            header ??= new MatchLineup { MatchId = matchId };
            var wasComplete = header.HomeLineupsReleased && header.AwayLineupsReleased && IsOfficial(header);

            var existing = await _db.MatchLineupPlayers.Where(p => p.MatchId == matchId).ToListAsync(ct);

            void WriteSide(string side, OfficialLineupSide s)
            {
                _db.MatchLineupPlayers.RemoveRange(existing.Where(p => p.Side == side));
                var subs = substitutions?.Where(x => string.Equals(x.Side, side, StringComparison.OrdinalIgnoreCase)).ToList();
                _db.MatchLineupPlayers.AddRange(Map(matchId, side, "Starter", s.Starters, subs));
                _db.MatchLineupPlayers.AddRange(Map(matchId, side, "Bench", s.Bench, subs));
            }

            // Önceki resmî olmayan (eski sağlayıcı) kadro satırları resmî veriyle karışmaz.
            if (!IsOfficial(header) && (header.HomeLineupsReleased || header.AwayLineupsReleased))
            {
                _db.MatchLineupPlayers.RemoveRange(existing);
                header.HomeLineupsReleased = false;
                header.AwayLineupsReleased = false;
                header.HomeFormation = null;
                header.AwayFormation = null;
                header.HomeCoach = null;
                header.AwayCoach = null;
            }

            if (verdict.Home.Accepted && doc.Home != null)
            {
                WriteSide("Home", doc.Home);
                header.HomeLineupsReleased = true;
                header.HomeFormation = doc.Home.Formation;
                header.HomeCoach = doc.Home.Coach;
            }
            if (verdict.Away.Accepted && doc.Away != null)
            {
                WriteSide("Away", doc.Away);
                header.AwayLineupsReleased = true;
                header.AwayFormation = doc.Away.Formation;
                header.AwayCoach = doc.Away.Coach;
            }

            var keys = (header.SourceKey ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!keys.Contains(doc.SourceKey)) keys.Add(doc.SourceKey);
            header.SourceKey = string.Join(',', keys);
            header.Provider = ProviderPrefix + doc.SourceKey;
            header.SourceUrl = doc.SourceUrl;
            header.RawContentHash = doc.ContentHash;
            header.SourcePublishedAtUtc = doc.PublishedAtUtc ?? header.SourcePublishedAtUtc;
            header.DiscoveredAtUtc ??= utcNow;
            header.VerifiedAtUtc = utcNow;
            header.ReleasedAt ??= utcNow;
            header.FetchedAt = utcNow;
            header.LastCheckedAtUtc = utcNow;
            var isComplete = header.HomeLineupsReleased && header.AwayLineupsReleased;
            header.VerificationStatus = isComplete ? "Verified" : "PartiallyVerified";
            header.DataQuality = DataQualityOf(doc, substitutions);
            if (backfill) header.BackfilledAtUtc = utcNow;

            if (isNew) _db.MatchLineups.Add(header);
            // Doğrulanmış resmî kadro → tahmin yenileme isteği AYNI işlemde (aynı içerik ikinci kez istek üretmez).
            // GEÇMİŞ DOLDURMADA İSTEK ÜRETİLMEZ: maç çoktan oynandı, yeniden hesaplama zaten
            // "KickoffPassed" ile atlanırdı; binlerce boş kuyruk satırı yazılmaz.
            if (!backfill)
                await Formax.Infrastructure.Outcomes.PredictionRecomputeQueue.EnqueueAsync(_db, matchId, "OfficialLineup", ProviderPrefix + doc.SourceKey,
                    $"lineup:{matchId}:{doc.SourceKey}:{header.HomeLineupsReleased}:{header.AwayLineupsReleased}:{doc.ContentHash}", utcNow, ct);
            await _db.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            _log.LogInformation("[OFFICIAL LINEUP] {MatchId} yazıldı ({Source}): ev {H} dep {A} — {Status}",
                matchId, doc.SourceKey, header.HomeLineupsReleased, header.AwayLineupsReleased, header.VerificationStatus);
            return (wasComplete, isComplete);
        }

        /// <summary>
        /// Kaynak satırını canonical satıra çevirir.
        ///
        /// DAKİKA POLİTİKASI: <c>SubstitutionMinute</c> yalnız kaynağın GERÇEKTEN yayımladığı
        /// değişiklik dakikasıdır (oyuncunun kendi olay listesinden ya da maçın değişiklik
        /// listesinden). <c>MinutesPlayed</c> yalnız İLK 11'de başlayıp çıkarılan oyuncu için
        /// yazılır — o zaman sahada geçirdiği süre yayımlanmış dakikaya EŞİTTİR. Çıkarılmayan
        /// oyuncuya 90 YAZILMAZ (maç süresi kaynakta yayımlanmıyor) ve oyuna giren oyuncuya da
        /// süre üretilmez.
        /// </summary>
        private static IEnumerable<MatchLineupPlayer> Map(
            int matchId, string side, string role, IEnumerable<OfficialLineupPlayer> players,
            IReadOnlyList<OfficialSubstitution>? substitutions = null)
            => players.Select(p =>
            {
                var minute = p.SubstitutionMinute;
                if (minute == null && p.OfficialPlayerId is { Length: > 0 } pid && substitutions != null)
                    minute = substitutions.FirstOrDefault(s => s.PlayerOffOfficialId == pid || s.PlayerOnOfficialId == pid)?.Minute;
                var startedAndReplaced = role == "Starter" && minute.HasValue;
                return new MatchLineupPlayer
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId,
                    Side = side,
                    Role = role,
                    ShirtNumber = p.ShirtNumber ?? 0,
                    PlayerName = p.Name.Length > 120 ? p.Name[..120] : p.Name,
                    Position = p.Position ?? string.Empty,
                    Grid = role == "Starter" ? p.Grid : null,
                    IsCaptain = p.IsCaptain,
                    OfficialPlayerId = p.OfficialPlayerId is { Length: > 80 } long80 ? long80[..80] : p.OfficialPlayerId,
                    SubstitutionMinute = minute,
                    MinutesPlayed = startedAndReplaced ? minute : null
                };
            });

        /// <summary>
        /// VERİ KALİTESİ SEVİYESİ — kadronun GERÇEKTEN taşıdığı alanlara göre. Eksik alan
        /// doldurulmuş gibi gösterilmez; seviye kullanıcıya ve ölçüme dürüstçe söylenir.
        /// </summary>
        public static string DataQualityOf(OfficialLineupDocument doc, IReadOnlyList<OfficialSubstitution>? substitutions)
        {
            var sides = new[] { doc.Home, doc.Away }.Where(s => s != null).Cast<OfficialLineupSide>().ToList();
            if (sides.Count == 0) return "StartersOnly";
            var hasMinutes = (substitutions?.Count ?? 0) > 0
                             || sides.Any(s => s.Starters.Concat(s.Bench).Any(p => p.SubstitutionMinute.HasValue));
            if (hasMinutes) return "WithMinutes";
            return sides.Any(s => s.Bench.Count > 0) ? "WithBench" : "StartersOnly";
        }

        private async Task<int> NotifyFollowersAsync(Match match, DateTime utcNow, CancellationToken ct)
        {
            var result = await _notifier.DispatchAsync(new MatchNotificationRequest(
                match.Id,
                MatchNotificationTypes.LineupAvailable,
                LineupTitle,
                LineupMessage(match.HomeTeam?.Name ?? string.Empty, match.AwayTeam?.Name ?? string.Empty),
                NotificationEventType.Lineup,
                userId => MatchNotificationTypes.LineupKey(match.Id, userId),
                utcNow,
                match.LeagueId), ct);

            var header = await _db.MatchLineups.FirstOrDefaultAsync(h => h.MatchId == match.Id, ct);
            if (header != null)
            {
                header.FollowersNotifiedAtUtc = utcNow;
                await _db.SaveChangesAsync(ct);
            }
            return result.Created;
        }

        /// <summary>
        /// Kadro yazıldı ama süreç dağıtımdan önce durduysa: son 6 saatte doğrulanmış, iki tarafı
        /// resmî ve dağıtımı işaretlenmemiş kadrolar için dağıtım tamamlanır (kullanıcı başına
        /// tekillik DB anahtarında olduğu için ikinci kez yazılmaz).
        /// </summary>
        private async Task<int> CompletePendingNotificationsAsync(DateTime utcNow, CancellationToken ct)
        {
            var since = utcNow.AddHours(-6);
            var pending = await _db.MatchLineups.AsNoTracking()
                .Where(h => h.HomeLineupsReleased && h.AwayLineupsReleased
                            && h.Provider != null && h.Provider.StartsWith(ProviderPrefix)
                            && h.FollowersNotifiedAtUtc == null
                            // GEÇMİŞ DOLDURMA BİLDİRİM ÜRETMEZ: oynanmış maçın kadrosu haber değildir.
                            && h.BackfilledAtUtc == null
                            && h.VerifiedAtUtc != null && h.VerifiedAtUtc >= since)
                .Select(h => h.MatchId)
                .ToListAsync(ct);
            var created = 0;
            foreach (var id in pending)
            {
                var match = await _db.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam)
                    .FirstOrDefaultAsync(m => m.Id == id, ct);
                if (match != null) created += await NotifyFollowersAsync(match, utcNow, ct);
            }
            return created;
        }
    }
}
