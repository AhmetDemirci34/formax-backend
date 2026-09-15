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
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    public sealed record RevalidationItem(long VideoId, int MatchId, string ExternalVideoId, string Title, string From, string To, string Reason);

    public sealed record RevalidationReport(int Checked, int Kept, int Reinstated, int Rejected, int SourceBlocked, int Requeued,
        IReadOnlyList<RevalidationItem> Changes);

    /// <summary>
    /// KALICI YENİDEN DOĞRULAMA — kabul edilmiş (Verified / EmbedBlocked) ve gol klibi olarak reddedilmiş kayıtları GÜNCEL
    /// kurallarla yeniden sınar. Yalnız elle düzeltilmiş üç kayıt değil, deftere yazılmış her kayıt 7 günde bir tekrar
    /// görülür (ilerleme <see cref="MatchVideo.RevalidatedAtUtc"/> ile kalıcıdır; restart kaybettirmez).
    ///
    /// Sıra: (1) kimlik/içerik kuralları (basın açıklaması, #shorts, altyapı, sezon, skor, yön, turnuva…);
    /// (2) resmî web kanıtı — eski RSS kaydı resmî sayfada aynı video kimliğiyle bulunursa korunur, bulunmazsa kullanıcıya
    /// gösterilmez; (3) oEmbed ile video hâlâ erişilebilir mi. Geçemeyen kayıt Rejected/SourceBlocked olur, gerekçesi
    /// deftere yazılır ve maç hemen yeniden arama kuyruğuna döner. Satır SİLİNMEZ.
    /// </summary>
    public sealed class MatchVideoRevalidationService
    {
        public static readonly TimeSpan RecheckAfter = TimeSpan.FromDays(7);

        private readonly FormaxDbContext _db;
        private readonly IMatchVideoRegistrar _registrar;
        private readonly IVideoEmbedVerifier _embed;
        private readonly IOfficialVideoSourceCatalog _catalog;
        private readonly VideoHttpBudget? _budget;
        private readonly ILogger<MatchVideoRevalidationService> _log;

        public MatchVideoRevalidationService(FormaxDbContext db, IMatchVideoRegistrar registrar, IVideoEmbedVerifier embed,
            IOfficialVideoSourceCatalog catalog, ILogger<MatchVideoRevalidationService> log, VideoHttpBudget? budget = null)
        {
            _db = db; _registrar = registrar; _embed = embed; _catalog = catalog; _log = log; _budget = budget;
        }

        public async Task<RevalidationReport> RunAsync(DateTime nowUtc, int max = 100, CancellationToken ct = default)
        {
            var threshold = nowUtc - RecheckAfter;
            var rows = await _db.MatchVideos
                .Where(v => (v.VerificationStatus == MatchVideoVerificationStatuses.Verified
                             || v.VerificationStatus == MatchVideoVerificationStatuses.EmbedBlocked
                             // Daha önce yalnız özet işareti olmadığı için reddedilen gol klipleri (Forest) yeniden incelenir.
                             || (v.VerificationStatus == MatchVideoVerificationStatuses.Rejected && v.VideoType == MatchVideoTypes.Goal
                                 && (v.RejectionReason == null || v.RejectionReason == MatchVideoRejectionReasons.NoHighlightMarker)))
                            && (v.RevalidatedAtUtc == null || v.RevalidatedAtUtc < threshold))
                .OrderBy(v => v.RevalidatedAtUtc ?? DateTime.MinValue).ThenBy(v => v.Id)
                .Take(Math.Clamp(max, 1, 1000))
                .ToListAsync(ct).ConfigureAwait(false);

            var sources = _catalog.Current();
            var changes = new List<RevalidationItem>();
            int kept = 0, reinstated = 0, rejected = 0, blocked = 0, requeued = 0;

            foreach (var v in rows)
            {
                ct.ThrowIfCancellationRequested();
                var from = v.VerificationStatus;
                var identity = await _registrar.BuildIdentityAsync(v.MatchId, ct).ConfigureAwait(false);
                if (identity == null) { v.RevalidatedAtUtc = nowUtc; continue; }

                var source = SourceFor(v, sources);
                var verdict = MatchVideoIdentityValidator.ValidateContent(v.Title, null, v.PublishedAtUtc ?? v.VerifiedAtUtc,
                    MatchVideoIdentityValidator.PrecisionExact, source, identity);

                string? rejectCode = null, rejectNote = null;
                if (!verdict.Accepted)
                {
                    rejectCode = MatchVideoRejectionReasons.FailedRevalidation;
                    rejectNote = verdict.Reason;
                }
                else if (!MatchVideoRules.HasOfficialWebEvidence(v))
                {
                    // Eski RSS kaydı: aynı YouTube kimliği bu maçın resmî kaynaklarından birinin sayfasında yayımlanmış mı?
                    var evidence = await WebEvidenceAsync(v, identity, ct).ConfigureAwait(false);
                    if (evidence == null)
                    {
                        rejectCode = MatchVideoRejectionReasons.RssOnlyEvidence;
                        rejectNote = "yalnız YouTube RSS kanıtı (robots.txt yasağı); resmî web sayfasında bulunamadı";
                    }
                    else
                    {
                        v.DiscoveryProvenance = MatchVideoRules.OfficialWebProvenance;
                        v.EvidencePageUrl = evidence.PageUrl.Length <= 1000 ? evidence.PageUrl : evidence.PageUrl[..1000];
                        v.EvidenceSourceKey = evidence.SourceKey;
                        reinstated++;
                    }
                }

                if (rejectCode != null)
                {
                    Close(v, MatchVideoVerificationStatuses.Rejected, rejectCode, rejectNote!, nowUtc);
                    rejected++;
                }
                else
                {
                    // Tür yeniden sınıflandırılır (tam özet ≠ gol klibi); gol klibinin dakikası/oyuncusu KANONİK golden.
                    if (verdict.VideoType != null && verdict.VideoType != v.VideoType) v.VideoType = verdict.VideoType;
                    if (verdict.Goal != null)
                    {
                        v.EventMinute = verdict.Goal.Minute; v.EventExtraMinute = verdict.Goal.ExtraMinute;
                        v.EventPlayer = verdict.Goal.PlayerName; v.EventTeam = verdict.Goal.HomeSide ? identity.HomeTeamName : identity.AwayTeamName;
                    }

                    if (_budget == null || _budget.TryConsume())
                    {
                        var candidate = new OfficialVideoCandidate("YouTube", v.EvidenceSourceKey ?? source.Key, v.ExternalVideoId, v.Title, null,
                            v.PublishedAtUtc ?? v.VerifiedAtUtc, v.SourcePageUrl, v.ThumbnailUrl, v.DurationSeconds, MatchId: v.MatchId,
                            ExternalFixtureId: v.ExternalFixtureId);
                        var embed = await _embed.VerifyAsync(candidate, source, ct).ConfigureAwait(false);
                        if (embed.Unavailable)
                        {
                            Close(v, MatchVideoVerificationStatuses.SourceBlocked, MatchVideoRejectionReasons.SourceUnavailable, embed.Reason, nowUtc);
                            blocked++;
                        }
                        else if (embed.Embeddable)
                        {
                            v.IsEmbeddable = true;
                            v.CanPlayInApp = !string.IsNullOrWhiteSpace(embed.EmbedUrl);
                            v.EmbedUrl = embed.EmbedUrl;
                            v.VerificationStatus = MatchVideoVerificationStatuses.Verified;
                            v.RejectionReason = null;
                            kept++;
                        }
                        else kept++;   // doğrulama ağ hatası: durum değişmez, sonraki turda yeniden sorulur
                    }
                    else kept++;
                }

                v.RevalidatedAtUtc = nowUtc;
                _db.MatchVideoDiscoveryAttempts.Add(Ledger(v, identity, nowUtc, from, rejectNote ?? verdict.Reason));
                // Kuyruk durumu DB'deki video durumundan hesaplanır: önce kapanış yazılır.
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                if (from != v.VerificationStatus || v.RejectionReason != null)
                {
                    changes.Add(new RevalidationItem(v.Id, v.MatchId, v.ExternalVideoId, v.Title, from, v.VerificationStatus, v.RejectionReason ?? "kept"));
                    if (!MatchVideoRules.IsPlayable(v))
                    {
                        await MatchVideoDiscoveryQueueService.RequeueAsync(_db, v.MatchId, "Revalidation:" + (v.RejectionReason ?? v.VerificationStatus), nowUtc, ct).ConfigureAwait(false);
                        requeued++;
                    }
                }
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            if (rows.Count > 0)
                _log.LogInformation("[VIDEO-REVALIDATE] kontrol={Checked} korunan={Kept} yenidenEtkin={Reinstated} red={Rejected} engelli={Blocked} kuyruk={Requeued}",
                    rows.Count, kept, reinstated, rejected, blocked, requeued);
            return new RevalidationReport(rows.Count, kept, reinstated, rejected, blocked, requeued, changes);
        }

        private static void Close(MatchVideo v, string status, string code, string note, DateTime nowUtc)
        {
            v.VerificationStatus = status;
            v.RejectionReason = code;
            v.CanPlayInApp = false;
            v.EmbedUrl = null;
            v.VerificationNote = (note.Length <= 400 ? note : note[..400]);
            if (status == MatchVideoVerificationStatuses.SourceBlocked) v.BlockedAtUtc = nowUtc;
        }

        /// <summary>Aynı YouTube kimliği maçın resmî kaynaklarından birinin sayfasında (kalıcı web girişleri) yayımlanmış mı?</summary>
        private async Task<OfficialWebVideoEntry?> WebEvidenceAsync(MatchVideo v, VideoFixtureIdentity f, CancellationToken ct)
        {
            var entries = await _db.OfficialWebVideoEntries.AsNoTracking()
                .Where(e => e.YouTubeVideoId == v.ExternalVideoId).ToListAsync(ct).ConfigureAwait(false);
            if (entries.Count == 0) return null;
            var keys = entries.Select(e => e.SourceKey).Distinct().ToList();
            var records = await _db.OfficialVideoSourceCatalog.AsNoTracking()
                .Where(r => keys.Contains(r.Key) && r.WebsiteStatus == OfficialVideoSourceCatalog.StatusVerified).ToListAsync(ct).ConfigureAwait(false);
            foreach (var e in entries)
            {
                var r = records.FirstOrDefault(x => x.Key == e.SourceKey);
                if (r == null) continue;
                var s = OfficialVideoSourceCatalog.ToSource(r);
                if (OfficialVideoSources.IsRelevant(s, f.HomeTeamName, f.AwayTeamName, f.LeagueId, f.HomeTeamId, f.AwayTeamId)) return e;
            }
            return null;
        }

        /// <summary>Kaydın kaynağı: web kanıtlıysa katalog anahtarı; eski kayıtta yayıncı adıyla eşleşen kaynak; yoksa nötr kaynak.</summary>
        private static OfficialVideoSource SourceFor(MatchVideo v, IReadOnlyList<OfficialVideoSource> sources)
            => (v.EvidenceSourceKey != null ? OfficialVideoSources.ByKey(v.EvidenceSourceKey, sources) : null)
               ?? sources.FirstOrDefault(s => string.Equals(s.Publisher, v.OfficialPublisher, StringComparison.OrdinalIgnoreCase))
               ?? new OfficialVideoSource("legacy:" + v.OfficialPublisher, v.OfficialPublisher, "YouTube", null, true, "eski kayıt",
                   OfficialVideoSourceTiers.LicensedSportsOutlet);

        private static MatchVideoDiscoveryAttempt Ledger(MatchVideo v, VideoFixtureIdentity f, DateTime nowUtc, string from, string note)
            => new()
            {
                MatchId = v.MatchId, ExternalFixtureId = v.ExternalFixtureId, HomeTeamId = f.HomeTeamId, AwayTeamId = f.AwayTeamId,
                LeagueId = f.LeagueId ?? 0, Season = f.MatchDateUtc.Month >= 7 ? f.MatchDateUtc.Year : f.MatchDateUtc.Year - 1,
                KickoffUtc = f.MatchDateUtc, EndUtc = MatchVideoIdentityValidator.EndOf(f.MatchDateUtc), AttemptNo = 0, AttemptedAtUtc = nowUtc,
                RowKind = "Revalidation", SourceKey = Cut(v.EvidenceSourceKey, 80), SourceKind = "Revalidation",
                SourceChannelOrDomain = Cut(v.OfficialPublisher, 200), SearchExpression = Cut("revalidate:" + v.ExternalVideoId, 300),
                CandidateUrl = Cut(v.EvidencePageUrl ?? v.SourcePageUrl, 600), CandidateTitle = Cut(v.Title, 300),
                CandidatePublishedUtc = v.PublishedAtUtc, VideoType = Cut(v.VideoType, 32), Accepted = MatchVideoRules.IsPlayable(v),
                VerificationStatus = Cut(v.VerificationStatus, 32), Evidence = Cut($"{from} → {v.VerificationStatus}", 600),
                RejectionReason = Cut(v.RejectionReason == null ? null : v.RejectionReason + ": " + note, 300)
            };

        private static string? Cut(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
    }

    /// <summary>
    /// OYNATICI HATA BİLDİRİMİ — gerçek kullanıcı oynatıcısı 100/101/150/152 (kaldırılmış/gizli/gömme/bölge engeli) ya da
    /// 2/5 bildirdiğinde kayıt SourceBlocked olur, gerekçesi deftere yazılır ve maç yeniden arama kuyruğuna döner.
    /// Bu çağrı keşif YAPMAZ (dış istek yok); aramayı arka plan işi yürütür. Yalnız o an oynatılabilir kayıt kapatılabilir.
    /// </summary>
    public sealed class MatchVideoPlaybackReportService
    {
        public static readonly IReadOnlySet<int> BlockingCodes = new HashSet<int> { 2, 5, 100, 101, 150, 152 };

        private readonly FormaxDbContext _db;
        private readonly ILogger<MatchVideoPlaybackReportService> _log;

        public MatchVideoPlaybackReportService(FormaxDbContext db, ILogger<MatchVideoPlaybackReportService> log)
        {
            _db = db; _log = log;
        }

        public sealed record Result(bool Accepted, string Outcome, string? QueueState, DateTime? NextAttemptUtc);

        public async Task<Result> ReportAsync(int matchId, string? videoKey, int code, DateTime nowUtc, CancellationToken ct = default)
        {
            if (!BlockingCodes.Contains(code)) return new Result(false, "IgnoredCode", null, null);
            var id = OfficialWebPageParser.ExtractYouTubeId(videoKey) ?? (videoKey != null && System.Text.RegularExpressions.Regex.IsMatch(videoKey, "^[A-Za-z0-9_-]{11}$") ? videoKey : null);
            if (id == null) return new Result(false, "UnknownVideo", null, null);

            var v = await _db.MatchVideos.FirstOrDefaultAsync(x => x.MatchId == matchId && x.ExternalVideoId == id, ct).ConfigureAwait(false);
            if (v == null) return new Result(false, "UnknownVideo", null, null);
            if (v.VerificationStatus == MatchVideoVerificationStatuses.SourceBlocked)
            {
                var q0 = await _db.MatchVideoDiscoveryQueue.AsNoTracking().FirstOrDefaultAsync(q => q.MatchId == matchId, ct).ConfigureAwait(false);
                return new Result(true, "AlreadyBlocked", q0?.State, q0?.NextAttemptUtc);
            }
            if (!MatchVideoRules.IsPlayable(v)) return new Result(false, "NotPlayable", null, null);

            v.VerificationStatus = MatchVideoVerificationStatuses.SourceBlocked;
            v.RejectionReason = MatchVideoRejectionReasons.PlayerError;
            v.PlayerErrorCode = code;
            v.BlockedAtUtc = nowUtc;
            v.CanPlayInApp = false;
            v.EmbedUrl = null;
            v.VerificationNote = $"oynatıcı hata kodu {code} (gömme/bölge engeli ya da kaldırılmış video)";
            _db.MatchVideoDiscoveryAttempts.Add(new MatchVideoDiscoveryAttempt
            {
                MatchId = v.MatchId, ExternalFixtureId = v.ExternalFixtureId, HomeTeamId = v.HomeTeamId, AwayTeamId = v.AwayTeamId,
                KickoffUtc = v.MatchDateUtc, EndUtc = MatchVideoIdentityValidator.EndOf(v.MatchDateUtc), AttemptedAtUtc = nowUtc,
                RowKind = "PlayerError", SourceKey = v.EvidenceSourceKey, SourceKind = "Player", SearchExpression = "player:" + id,
                ErrorType = "YT" + code, CandidateUrl = v.EvidencePageUrl ?? v.SourcePageUrl, CandidateTitle = v.Title.Length <= 300 ? v.Title : v.Title[..300],
                VideoType = v.VideoType, Accepted = false, VerificationStatus = MatchVideoVerificationStatuses.SourceBlocked,
                RejectionReason = "PlayerError:" + code
            });
            // Önce kapanış yazılır: kuyruk durumu DB'deki video durumundan hesaplanır.
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await MatchVideoDiscoveryQueueService.RequeueAsync(_db, matchId, "PlayerError:" + code, nowUtc, ct).ConfigureAwait(false);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            var q = await _db.MatchVideoDiscoveryQueue.AsNoTracking().FirstOrDefaultAsync(x => x.MatchId == matchId, ct).ConfigureAwait(false);
            _log.LogWarning("[VIDEO-PLAYER] {MatchId} {VideoId} oynatıcı hatası {Code} → SourceBlocked; kuyruk={State} sonraki={Next}",
                matchId, id, code, q?.State, q?.NextAttemptUtc);
            return new Result(true, "Blocked", q?.State, q?.NextAttemptUtc);
        }
    }
}
