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
    /// <summary>
    /// MatchVideos TABLOSUNA YAZAN TEK KAPI.
    ///
    /// Sıra sabittir ve kısayolu yoktur:
    ///   1) Maç kimliği kurulur (fikstür kimliği, ev/deplasman yönü, diğer ayaklar).
    ///   2) <see cref="MatchVideoIdentityValidator"/> — resmî kaynak + doğru maç + tür.
    ///   3) TEKİLLEŞTİRME — aynı video ikinci kez yazılmaz.
    ///   4) <see cref="IVideoEmbedVerifier"/> — uygulama içi oynatma izni.
    ///   5) Kayıt: oynatılabilirlik kararı YAZMA anında donar.
    ///
    /// 2. adım geçilmeden 4. adıma gidilmez: doğrulanmamış bir adaya embed sorgusu atmak
    /// hem boşuna dış istektir hem de "resmî mi?" sorusunu "oynuyor mu?" sorusuna
    /// indirger. Korsan yükleme de gayet güzel oynar.
    /// </summary>
    public sealed class MatchVideoRegistrar : IMatchVideoRegistrar
    {
        private readonly FormaxDbContext _db;
        private readonly IVideoEmbedVerifier _embedVerifier;
        private readonly ILogger<MatchVideoRegistrar> _log;

        private readonly IOfficialVideoSourceCatalog? _catalog;

        public MatchVideoRegistrar(
            FormaxDbContext db, IVideoEmbedVerifier embedVerifier, ILogger<MatchVideoRegistrar> log,
            IOfficialVideoSourceCatalog? catalog = null)
        {
            _db = db; _embedVerifier = embedVerifier; _log = log; _catalog = catalog;
        }

        /// <summary>
        /// Maçın kimlik kanıtları. DİĞER AYAKLAR aynı iki takımın bütün karşılaşmalarıdır —
        /// rövanşı ayırmanın tek deterministik yolu budur.
        /// </summary>
        public async Task<VideoFixtureIdentity?> BuildIdentityAsync(int matchId, CancellationToken ct = default)
        {
            var match = await _db.Matches.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == matchId, ct).ConfigureAwait(false);
            if (match == null || string.IsNullOrWhiteSpace(match.ExternalMatchId)) return null;

            var names = await _db.Teams.AsNoTracking()
                .Where(t => t.Id == match.HomeTeamId || t.Id == match.AwayTeamId)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync(ct).ConfigureAwait(false);

            var home = names.FirstOrDefault(t => t.Id == match.HomeTeamId)?.Name;
            var away = names.FirstOrDefault(t => t.Id == match.AwayTeamId)?.Name;
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) return null;

            var otherLegs = await _db.Matches.AsNoTracking()
                .Where(m => m.Id != matchId
                         && ((m.HomeTeamId == match.HomeTeamId && m.AwayTeamId == match.AwayTeamId)
                          || (m.HomeTeamId == match.AwayTeamId && m.AwayTeamId == match.HomeTeamId)))
                .Select(m => m.MatchDate)
                .ToListAsync(ct).ConfigureAwait(false);

            return new VideoFixtureIdentity(
                match.Id, match.ExternalMatchId!, match.MatchDate,
                match.HomeTeamId, match.AwayTeamId, home!, away!, otherLegs,
                match.LeagueId,
                // Skor yalnız maç bittiyse kanıttır; bitmemiş maçın 0-0 varsayılanı karşılaştırılmaz.
                match.Status == MatchStatuses.Finished ? match.HomeScore : null,
                match.Status == MatchStatuses.Finished ? match.AwayScore : null);
        }

        public async Task<MatchVideoRegistration> RegisterAsync(
            int matchId, OfficialVideoCandidate candidate, CancellationToken ct = default)
        {
            var fixture = await BuildIdentityAsync(matchId, ct).ConfigureAwait(false);
            if (fixture == null)
                return new MatchVideoRegistration(false, "Rejected", "maç veya fikstür kimliği bulunamadı");

            // ── KİMLİK ───────────────────────────────────────────────────────────
            // Resmî kaynak listesi = yayın hakkı tohumları + otomatik doğrulanmış katalog.
            var verdict = MatchVideoIdentityValidator.Validate(candidate, fixture, _catalog?.Current());
            if (!verdict.Accepted || verdict.Source == null || verdict.VideoType == null)
                return new MatchVideoRegistration(false, "Rejected", verdict.Reason);

            // ── TEKİLLEŞTİRME ────────────────────────────────────────────────────
            // İki anahtar birden: (maç + kaynak video kimliği) ve kanonik kaynak adresi.
            // Aynı video farklı adresle (parametreli/paylaşım linki) gelebilir.
            var canonical = Canonicalize(candidate.SourcePageUrl);
            var existing = await _db.MatchVideos
                .Where(v => v.MatchId == matchId
                         && (v.ExternalVideoId == candidate.ExternalVideoId
                          || v.SourcePageUrl == canonical))
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (existing != null)
                return new MatchVideoRegistration(false, "Duplicate", "bu video zaten kayıtlı", existing.Id);

            // ── AYNI VİDEO İKİ FARKLI MAÇA BAĞLANAMAZ ────────────────────────────
            // ÖLÇÜLDÜ (03.09.2026): tek bir TRT SPOR stüdyo programı (29GROlpBfYo) hem
            // 82549 hem 103619 maçına özet diye bağlanmıştı. Bir maç görüntüsü tanımı
            // gereği TEK maça aittir; aynı kimliğin ikinci bir maça bağlanması, kaydın
            // maç özeti OLMADIĞININ güçlü işaretidir.
            var boundElsewhere = await _db.MatchVideos.AsNoTracking()
                .AnyAsync(v => v.ExternalVideoId == candidate.ExternalVideoId && v.MatchId != matchId, ct)
                .ConfigureAwait(false);
            if (boundElsewhere)
                return new MatchVideoRegistration(false, MatchVideoVerificationStatuses.Rejected,
                    "aynı video başka bir maça bağlı; tek maça ait olmayan içerik kabul edilmez");

            // ── EMBED İZNİ ───────────────────────────────────────────────────────
            var embed = await _embedVerifier.VerifyAsync(candidate, verdict.Source, ct).ConfigureAwait(false);
            var canPlay = embed.Embeddable && !string.IsNullOrWhiteSpace(embed.EmbedUrl);

            var countries = (candidate.AvailableCountries ?? Array.Empty<string>())
                .Select(c => (c ?? string.Empty).Trim().ToUpperInvariant())
                .Where(c => c.Length == 2)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var row = new MatchVideo
            {
                MatchId            = matchId,
                ExternalFixtureId  = fixture.ExternalFixtureId,
                MatchDateUtc       = fixture.MatchDateUtc,
                HomeTeamId         = fixture.HomeTeamId,
                AwayTeamId         = fixture.AwayTeamId,
                ExternalVideoId    = candidate.ExternalVideoId,
                Title              = Trim(candidate.Title, 300),
                OfficialPublisher  = Trim(verdict.Source.Publisher, 120),
                SourcePageUrl      = Trim(canonical, 600),
                EmbedUrl           = canPlay ? TrimOrNull(embed.EmbedUrl, 600) : null,
                ThumbnailUrl       = TrimOrNull(embed.ThumbnailUrl ?? candidate.ThumbnailUrl, 600),
                DurationSeconds    = candidate.DurationSeconds,
                VideoType          = verdict.VideoType,
                PublishedAtUtc     = candidate.PublishedUtc,
                IsOfficial         = true,
                IsEmbeddable       = embed.Embeddable,
                CanPlayInApp       = canPlay,
                // BÖLGESEL KISIT: kaynak ülke listesi verdiyse olduğu gibi saklanır.
                // Liste boşsa kısıt "yok" değil, BİLİNMİYOR demektir — o yüzden
                // IsRegionRestricted yalnız liste doluyken true olur.
                AvailableCountries = countries.Count == 0 ? null : string.Join(",", countries),
                IsRegionRestricted = countries.Count > 0,
                EventMinute        = candidate.EventMinute,
                EventExtraMinute   = candidate.EventExtraMinute,
                EventPlayer        = TrimOrNull(candidate.EventPlayer, 120),
                EventTeam          = TrimOrNull(candidate.EventTeam, 120),
                VerificationStatus = canPlay
                    ? MatchVideoVerificationStatuses.Verified
                    : MatchVideoVerificationStatuses.EmbedBlocked,
                // Kabul edilen kayıtta gerekçe BOŞTUR: dolu bir gerekçe her zaman
                // "gösterme" demektir ve okuma yolu buna bakar.
                RejectionReason    = null,
                VerificationNote   = Trim($"{verdict.Reason} | embed: {embed.Reason}", 400),
                VerifiedAtUtc      = DateTime.UtcNow
            };

            _db.MatchVideos.Add(row);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.LogInformation(
                "[POST-MATCH VIDEO] {MatchId} — {Publisher} / {Type} kaydedildi (oynatilabilir={CanPlay}).",
                matchId, row.OfficialPublisher, row.VideoType, canPlay);

            return new MatchVideoRegistration(true, row.VerificationStatus, row.VerificationNote, row.Id);
        }

        /// <summary>
        /// Kanonik adres — tekilleştirme anahtarı. İzleme/paylaşım parametreleri
        /// (<c>?si=</c>, <c>&amp;t=</c>, <c>utm_*</c>) aynı videoyu farklı kayıt gibi
        /// gösterir; bu yüzden sorgu dizesi ve son eğik çizgi atılır.
        /// </summary>
        public static string Canonicalize(string? url)
        {
            var raw = (url ?? string.Empty).Trim();
            if (raw.Length == 0) return string.Empty;
            var q = raw.IndexOf('?');
            // YouTube izleme adresinde ?v= KİMLİĞİN kendisidir; atılamaz.
            if (q > 0 && !raw.Contains("/watch?", StringComparison.OrdinalIgnoreCase))
                raw = raw[..q];
            return raw.TrimEnd('/');
        }

        /// <summary>Kolon sınırına kısaltır; null gelirse boş dizeye iner.</summary>
        private static string Trim(string? value, int max)
        {
            var v = value ?? string.Empty;
            return v.Length <= max ? v : v[..max];
        }

        /// <summary>Boş değeri null olarak korur (kolon nullable ise "" yazılmasın).</summary>
        private static string? TrimOrNull(string? value, int max)
            => string.IsNullOrWhiteSpace(value) ? null : Trim(value, max);
    }
}
