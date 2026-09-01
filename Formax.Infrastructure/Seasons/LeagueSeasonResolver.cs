using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using Formax.Application.Interfaces;
using Formax.Application.Services.Seasons;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Formax.Infrastructure.Seasons
{
    /// <summary>
    /// <see cref="ILeagueSeasonResolver"/> uygulaması — sezon başlangıcı YALNIZ METADATA'dan.
    ///
    /// KAYNAK ÖNCELİĞİ:
    ///   1) <c>LeagueSeasons</c> tablosu — doğrulanmış sezon metadata kaydı (LeagueId+SeasonYear).
    ///   2) Açık yapılandırma — <c>LeagueSeasons:{leagueId}:{seasonYear}:Start</c> (ISO tarih).
    /// Hiçbiri yoksa: <b>SEASON_START_UNRESOLVED</b>. Tarih TAHMİN EDİLMEZ.
    ///
    /// DEPODAKİ İLK FİKSTÜR ARTIK YETKİLİ DEĞİLDİR (30.08.2026 düzeltmesi). Eksik veya geç
    /// giren bir fikstür sezon penceresini sessizce kaydırıyor, bütün form ve puan hesabını
    /// yanlış aralığa taşıyordu. İlk fikstür yalnız teşhis alanı olarak taşınır
    /// (<see cref="LeagueSeasonScope.DiagnosticFirstFixtureUtc"/>) ve hesapta KULLANILMAZ.
    ///
    /// Sezon KOVASI (hangi yılın sezonu) için Temmuz kırılımı kullanılır; bu bir başlangıç
    /// tarihi değil, yalnız "hangi sezona bakıyoruz" eşlemesidir ve projede zaten vardı.
    /// </summary>
    public sealed class LeagueSeasonResolver : ILeagueSeasonResolver
    {
        private readonly FormaxDbContext _db;
        private readonly IConfiguration _config;

        private static readonly ConcurrentDictionary<(int LeagueId, int SeasonYear), LeagueSeasonResolution> Cache = new();

        public LeagueSeasonResolver(FormaxDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        /// <summary>Sezon KOVASI — projede zaten kullanılan Temmuz kırılımı, tek yerde.</summary>
        public int SeasonYearOf(DateTime utc) => utc.Month >= 7 ? utc.Year : utc.Year - 1;

        public LeagueSeasonResolution Resolve(int leagueId, DateTime referenceUtc)
        {
            var seasonYear = SeasonYearOf(referenceUtc);

            if (Cache.TryGetValue((leagueId, seasonYear), out var cached)) return cached;

            var computed = Compute(leagueId, seasonYear);

            // BAŞARISIZLIK ÖNBELLEĞE ALINMAZ: metadata kaydı operasyon tarafından sonradan
            // girilebilir; negatif sonucu saklamak, kayıt eklendikten sonra yeniden başlatma
            // gerektiriyordu (ölçüldü: 6 lig metadata girildi, çalışan örnek görmedi).
            if (computed.Resolved) Cache[(leagueId, seasonYear)] = computed;

            return computed;
        }

        /// <summary>Metadata değiştiğinde/yeni sonuç geldiğinde önbelleği düşürür.</summary>
        public static void Invalidate(int leagueId, int seasonYear) => Cache.TryRemove((leagueId, seasonYear), out _);

        private LeagueSeasonResolution Compute(int leagueId, int seasonYear)
        {
            // Kova sınırı — resmî bitiş bilinmiyorsa kapsamın üst sınırı olarak kullanılır.
            var bucketEnd = new DateTime(seasonYear + 1, 7, 1, 0, 0, 0, DateTimeKind.Utc);

            // TEŞHİS: depodaki ilk fikstür. Hesapta KULLANILMAZ; yalnız kaymayı görmek için.
            var bucketStart = new DateTime(seasonYear, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            var firstFixture = _db.Matches.AsNoTracking()
                .Where(m => m.LeagueId == leagueId && m.MatchDate >= bucketStart && m.MatchDate < bucketEnd)
                .OrderBy(m => m.MatchDate)
                .Select(m => (DateTime?)m.MatchDate)
                .FirstOrDefault();

            // 1) DOĞRULANMIŞ METADATA KAYDI
            var meta = _db.LeagueSeasons.AsNoTracking()
                .FirstOrDefault(s => s.LeagueId == leagueId && s.SeasonYear == seasonYear);

            if (meta != null)
            {
                return LeagueSeasonResolution.Ok(new LeagueSeasonScope(
                    leagueId,
                    seasonYear,
                    DateTime.SpecifyKind(meta.StartUtc, DateTimeKind.Utc),
                    meta.EndUtc.HasValue ? DateTime.SpecifyKind(meta.EndUtc.Value, DateTimeKind.Utc) : bucketEnd,
                    "SeasonMetadata",
                    firstFixture));
            }

            // 2) AÇIK YAPILANDIRMA (operasyon kararı)
            var configured = _config[$"LeagueSeasons:{leagueId}:{seasonYear}:Start"];
            if (!string.IsNullOrWhiteSpace(configured) &&
                DateTime.TryParse(configured, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var configuredStart))
            {
                return LeagueSeasonResolution.Ok(new LeagueSeasonScope(
                    leagueId, seasonYear, configuredStart, bucketEnd, "Configuration", firstFixture));
            }

            // 3) ÇÖZÜLEMEDİ — tahmin yok, önceki sezondan doldurma yok.
            var diag = firstFixture.HasValue
                ? $" (teşhis: depodaki ilk fikstür {firstFixture:yyyy-MM-dd} — resmî başlangıç DEĞİLDİR)"
                : " (depoda bu sezon penceresinde hiç fikstür yok)";

            return LeagueSeasonResolution.Fail(
                $"SEASON_START_UNRESOLVED: LeagueId={leagueId} SeasonId={seasonYear} için doğrulanmış " +
                $"sezon metadata kaydı yok. LeagueSeasons tablosuna kayıt girilmeli.{diag}");
        }
    }
}
