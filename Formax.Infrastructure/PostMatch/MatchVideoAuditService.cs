using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>Bir taramanın sonucu — önce salt okunur rapor, istenirse düzeltme.</summary>
    public sealed class MatchVideoAuditReport
    {
        public int Total { get; set; }
        public int Verified { get; set; }
        public int EmbedBlocked { get; set; }
        public int Rejected { get; set; }
        public int NeedsManualReview { get; set; }

        /// <summary>Başlığında birden çok karşılaşma listeleyen kayıtlar.</summary>
        public int MultipleMatchesInTitle { get; set; }

        /// <summary>Karşılaşmanın yalnız bir tarafı başlıkta geçen kayıtlar.</summary>
        public int OnlyOneTeamInTitle { get; set; }

        /// <summary>Başlığında gerçek özet işareti (Özet/Highlights) olmayan kayıtlar.</summary>
        public int NoHighlightMarker { get; set; }

        /// <summary>Aynı ExternalVideoId'nin bağlandığı FARKLI maç sayısı (1'den büyükse sorun).</summary>
        public int VideoIdsSharedAcrossMatches { get; set; }

        /// <summary>Bu turda durumu değişen kayıtlar (teşhis; kimlikleriyle).</summary>
        public List<MatchVideoAuditChange> Changes { get; set; } = new();
    }

    public sealed class MatchVideoAuditChange
    {
        public long Id { get; set; }
        public int MatchId { get; set; }
        public string ExternalVideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string FromStatus { get; set; } = string.Empty;
        public string ToStatus { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// GERİYE DÖNÜK YANLIŞ EŞLEŞME TARAMASI.
    ///
    /// NEDEN GEREKLİ: sınıflandırıcı sertleştirildi ama DEPODAKİ kayıtlar eski gevşek
    /// kuralla yazıldı. Kod düzeltmesi tek başına, zaten yazılmış yanlış bir kaydı
    /// ekrandan kaldırmaz.
    ///
    /// ÖLÇÜLDÜ (03.09.2026): tek bir TRT SPOR stüdyo programı (29GROlpBfYo,
    /// "…Beşiktaş - Çorum FK, Amedspor - Trabzonspor | Stadyum") İKİ ayrı maça
    /// (82549, 103619) "gol klibi" olarak bağlanmıştı; başlıkta "gol" geçmesi yetmişti.
    ///
    /// İKİ AYRI KOVA — karıştırılmaz:
    ///  • KANITLANMIŞ YANLIŞ → <c>Rejected</c>. Stüdyo programı, başlıkta birden çok
    ///    karşılaşma, aynı videonun iki maça bağlı olması. Bunlar tartışmaya açık değil.
    ///  • KANIT YETERSİZ → <c>NeedsManualReview</c>. Özet işareti yok ya da yalnız bir
    ///    takım geçiyor. Kayıt SİLİNMEZ; insan bakabilsin diye durur, kullanıcıya ise
    ///    gösterilmez.
    ///
    /// Hiçbir satır SİLİNMEZ ve doğrulanmış doğru kayıtlara DOKUNULMAZ.
    /// </summary>
    public sealed class MatchVideoAuditService
    {
        private readonly FormaxDbContext _db;
        private readonly ILogger<MatchVideoAuditService> _log;

        public MatchVideoAuditService(FormaxDbContext db, ILogger<MatchVideoAuditService> log)
        {
            _db = db; _log = log;
        }

        /// <param name="apply">
        /// false = SALT OKUNUR rapor (hiçbir satır değişmez). true = yalnız yukarıdaki
        /// iki kovaya düşen kayıtlar işaretlenir.
        /// </param>
        public async Task<MatchVideoAuditReport> RunAsync(bool apply, CancellationToken ct = default)
        {
            var videos = await _db.MatchVideos.ToListAsync(ct).ConfigureAwait(false);
            var report = new MatchVideoAuditReport { Total = videos.Count };
            if (videos.Count == 0) return report;

            // Aynı kaynak videosunun kaç FARKLI maça bağlandığı — bir maç görüntüsü
            // tanımı gereği tek maça aittir.
            var sharedIds = videos
                .GroupBy(v => v.ExternalVideoId, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(x => x.MatchId).Distinct().Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            report.VideoIdsSharedAcrossMatches = sharedIds.Count;

            var matchIds = videos.Select(v => v.MatchId).Distinct().ToList();
            var teamNames = await (
                from m in _db.Matches.AsNoTracking().Where(m => matchIds.Contains(m.Id))
                join ht in _db.Teams.AsNoTracking() on m.HomeTeamId equals ht.Id into htj
                from home in htj.DefaultIfEmpty()
                join at in _db.Teams.AsNoTracking() on m.AwayTeamId equals at.Id into atj
                from away in atj.DefaultIfEmpty()
                select new { m.Id, Home = home != null ? home.Name : null, Away = away != null ? away.Name : null }
            ).ToListAsync(ct).ConfigureAwait(false);
            var names = teamNames.ToDictionary(x => x.Id, x => (x.Home, x.Away));

            foreach (var v in videos)
            {
                ct.ThrowIfCancellationRequested();

                var folded = MatchVideoIdentityValidator.Fold(v.Title);
                var fixtures = MatchVideoIdentityValidator.CountsDistinctFixtures(v.Title);
                var hasMarker = MatchVideoIdentityValidator.HasHighlightMarker(folded);
                var isStudio = MatchVideoIdentityValidator.IsStudioContent(folded);

                var bothTeams = true;
                if (names.TryGetValue(v.MatchId, out var n) && n.Home != null && n.Away != null)
                    bothTeams = MatchVideoIdentityValidator.MentionsTeam(folded, n.Home)
                             && MatchVideoIdentityValidator.MentionsTeam(folded, n.Away);

                if (fixtures > 1) report.MultipleMatchesInTitle++;
                if (!bothTeams) report.OnlyOneTeamInTitle++;
                if (!hasMarker) report.NoHighlightMarker++;

                // ── KANITLANMIŞ YANLIŞ ───────────────────────────────────────────
                string? rejectReason =
                      isStudio                        ? MatchVideoRejectionReasons.NotMatchHighlights
                    : fixtures > 1                    ? MatchVideoRejectionReasons.MultipleMatchesInTitle
                    : sharedIds.Contains(v.ExternalVideoId) ? MatchVideoRejectionReasons.SharedAcrossMatches
                    : null;

                // ── KANIT YETERSİZ ───────────────────────────────────────────────
                string? reviewReason = rejectReason != null ? null
                    : !hasMarker  ? MatchVideoRejectionReasons.NoHighlightMarker
                    : !bothTeams  ? MatchVideoRejectionReasons.TeamsNotConfirmed
                    : null;

                var target = rejectReason != null
                    ? MatchVideoVerificationStatuses.Rejected
                    : reviewReason != null
                        ? MatchVideoVerificationStatuses.NeedsManualReview
                        : v.VerificationStatus;
                var reasonCode = rejectReason ?? reviewReason;

                if (target != v.VerificationStatus || (reasonCode ?? "") != (v.RejectionReason ?? ""))
                {
                    report.Changes.Add(new MatchVideoAuditChange
                    {
                        Id = v.Id, MatchId = v.MatchId, ExternalVideoId = v.ExternalVideoId,
                        Title = v.Title, FromStatus = v.VerificationStatus,
                        ToStatus = target, Reason = reasonCode ?? string.Empty
                    });

                    if (apply && reasonCode != null)
                    {
                        v.VerificationStatus = target;
                        v.RejectionReason = reasonCode;
                        // Kapanan kayıt ASLA oynatılmaz; gömme adresi de bırakılmaz ki
                        // ileride bir yüzey onu yanlışlıkla kullanamasın.
                        v.CanPlayInApp = false;
                        v.EmbedUrl = null;
                        v.VerifiedAtUtc = DateTime.UtcNow;
                    }
                }

                var effective = apply && reasonCode != null ? target : v.VerificationStatus;
                switch (effective)
                {
                    case MatchVideoVerificationStatuses.Verified: report.Verified++; break;
                    case MatchVideoVerificationStatuses.EmbedBlocked: report.EmbedBlocked++; break;
                    case MatchVideoVerificationStatuses.Rejected: report.Rejected++; break;
                    case MatchVideoVerificationStatuses.NeedsManualReview: report.NeedsManualReview++; break;
                }
            }

            if (apply && report.Changes.Count > 0)
            {
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                _log.LogWarning("[POST-MATCH VIDEO] Denetim: {Count} kayit kapatildi.", report.Changes.Count);
            }

            return report;
        }
    }
}
