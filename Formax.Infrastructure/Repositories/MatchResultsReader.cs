using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;
using Formax.Application.Services.Matches;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// SONUÇ LİSTESİNİN OKUMA YOLU — SALT DB.
    ///
    /// Bu sınıf hiçbir sağlayıcıya çıkmaz: "Maçlar → SONUÇLAR" sekmesi, gün değişimi ve
    /// kart tıklaması api-football kotasına DOKUNMAZ. Veriler fikstür senkronunun zaten
    /// yazdığı satırlardır.
    ///
    /// ÜÇ SESSİZ TUZAK VE ÇÖZÜMLERİ:
    ///
    /// 1) DURUM SAATTEN TÜRETİLEMEZ. Yaklaşan-maç listesi "kickoff geçtiyse bitmiştir"
    ///    varsayar; sonuç listesi bunu yapamaz — ertelenen, iptal olan ya da uzayan maç
    ///    saatine bakılarak "bitti" sayılamaz. Burada kaynak yalnız
    ///    <see cref="MatchStatuses.Finished"/> alanıdır.
    ///
    /// 2) GÜN SINIRI UTC DEĞİLDİR. "26 Ağustos maçları" Türkiye takvim günüdür; UTC'de
    ///    25.08 21:00 – 26.08 21:00 aralığına düşer. Avrupa kupası maçlarının çoğu tam o
    ///    sınırın çevresinde oynandığı için UTC günü kullanmak maçları bir gün kaydırır.
    ///
    /// 3) KAPSAM DIŞI VE ARTIK SATIRLAR. Depoda geçmiş ingestion'lardan kalan
    ///    <c>LeagueId=0</c> satırları (ölçüldü 03.09.2026: 21 bitmiş kayıt) ve kilitli
    ///    kapsam dışı ligler DURUYOR. Kilitli 11 organizasyon süzgeci bunları eler;
    ///    ayrıca aynı fikstür iki satıra düşmüşse kullanıcıya BİR kez gösterilir.
    /// </summary>
    public sealed class MatchResultsReader : IMatchResultsReader
    {
        private readonly FormaxDbContext _db;
        private readonly ILeagueSeasonResolver _seasonResolver;
        private readonly IMemoryCache _cache;

        public MatchResultsReader(
            FormaxDbContext db, ILeagueSeasonResolver seasonResolver, IMemoryCache cache)
        {
            _db = db; _seasonResolver = seasonResolver; _cache = cache;
        }

        public async Task<List<MatchResultItemDto>> GetResultsAsync(
            DateOnly istanbulDay, CancellationToken ct = default)
        {
            var (startUtc, endUtc) = IstanbulCalendar.DayRangeUtc(istanbulDay);
            var locked = LockedCompetitions.All.ToList();

            var rows = await (
                from m in _db.Matches.AsNoTracking()
                where m.Status == MatchStatuses.Finished
                   && m.MatchDate >= startUtc && m.MatchDate < endUtc
                   && locked.Contains(m.LeagueId)
                join ht in _db.Teams.AsNoTracking() on m.HomeTeamId equals ht.Id into htj
                from home in htj.DefaultIfEmpty()
                join at in _db.Teams.AsNoTracking() on m.AwayTeamId equals at.Id into atj
                from away in atj.DefaultIfEmpty()
                select new
                {
                    m.Id, m.ExternalMatchId, m.LeagueId, m.League, m.Round, m.MatchDate,
                    m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore,
                    m.HalfTimeHomeScore, m.HalfTimeAwayScore, m.Status,
                    HomeName = home != null ? home.Name : null,
                    HomeLogo = home != null ? home.LogoUrl : null,
                    AwayName = away != null ? away.Name : null,
                    AwayLogo = away != null ? away.LogoUrl : null
                }).ToListAsync(ct).ConfigureAwait(false);

            if (rows.Count == 0) return new List<MatchResultItemDto>();

            // TEKİLLEŞTİRME — aynı fikstür iki satıra düşmüşse kullanıcıya BİR kez çıkar.
            // Anahtar fikstür kimliğidir; kimliği olmayan satır kendi MatchId'siyle tekildir.
            // Aynı kimlikten en küçük MatchId seçilir → sıralama çalıştırmalar arası SABİT.
            var deduped = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.ExternalMatchId)
                    ? $"id:{r.Id}"
                    : $"fx:{r.ExternalMatchId!.Trim()}")
                .Select(g => g.OrderBy(x => x.Id).First())
                .ToList();

            // Oynatılabilir resmî video işareti — TEK sorgu, kart başına sorgu yok.
            //
            // KURAL EKRANLA AYNI YERDEN GELİR (<see cref="MatchVideoRules.IsPlayable"/>):
            // Rejected, NeedsManualReview, gerekçesi dolu ya da gömme adresi olmayan
            // hiçbir kayıt "Video var" işareti ÜRETMEZ. Kart "var" derken ekranın boş
            // kalması, kullanıcı için ürünün bozuk olduğu anlamına gelir.
            var matchIds = deduped.Select(r => r.Id).ToList();
            var videoRows = await _db.MatchVideos.AsNoTracking()
                .Where(v => matchIds.Contains(v.MatchId))
                .ToListAsync(ct).ConfigureAwait(false);
            var playableSet = new HashSet<int>(
                videoRows.Where(MatchVideoRules.IsPlayable).Select(v => v.MatchId).Distinct());

            return deduped
                // DETERMİNİSTİK SIRA (ürün kararı 13.09.2026): EN SON BİTEN ÖNCE — kickoff
                // azalan, eşitlikte MatchId azalan. Aynı dakikada başlayan maçlar her
                // açılışta AYNI sırada görünür.
                .OrderByDescending(r => r.MatchDate)
                .ThenByDescending(r => r.Id)
                .Select(r =>
                {
                    var round = string.IsNullOrWhiteSpace(r.Round) ? null : r.Round!.Trim();
                    return new MatchResultItemDto
                    {
                        MatchId           = r.Id,
                        ExternalFixtureId = string.IsNullOrWhiteSpace(r.ExternalMatchId) ? null : r.ExternalMatchId,
                        LeagueId          = r.LeagueId,
                        LeagueName        = r.League ?? string.Empty,
                        Round             = round,
                        MatchTypeLabel    = MatchTypeLabelResolver.Resolve(round),
                        MatchDateUtc      = r.MatchDate,
                        HomeTeam = new MatchResultTeamDto
                        {
                            TeamId = r.HomeTeamId, Name = r.HomeName ?? string.Empty, LogoUrl = r.HomeLogo
                        },
                        AwayTeam = new MatchResultTeamDto
                        {
                            TeamId = r.AwayTeamId, Name = r.AwayName ?? string.Empty, LogoUrl = r.AwayLogo
                        },
                        HomeScore              = r.HomeScore,
                        AwayScore              = r.AwayScore,
                        HalfTimeHomeScore      = r.HalfTimeHomeScore,
                        HalfTimeAwayScore      = r.HalfTimeAwayScore,
                        Status                 = r.Status,
                        HasPlayableOfficialVideo = playableSet.Contains(r.Id)
                    };
                })
                .ToList();
        }

        public async Task<List<MatchResultDayDto>> GetRecentResultDaysAsync(
            int days, CancellationToken ct = default)
        {
            var span = Math.Clamp(days, 1, 31);
            var today = IstanbulCalendar.TodayIn(DateTime.UtcNow);
            var oldest = today.AddDays(-(span - 1));

            var (fromUtc, _) = IstanbulCalendar.DayRangeUtc(oldest);
            var (_, toUtc) = IstanbulCalendar.DayRangeUtc(today);
            var locked = LockedCompetitions.All.ToList();

            // Pencerenin tamamı TEK sorguda çekilir; gün başına ayrı sorgu atılmaz.
            var rows = await _db.Matches.AsNoTracking()
                .Where(m => m.Status == MatchStatuses.Finished
                         && m.MatchDate >= fromUtc && m.MatchDate < toUtc
                         && locked.Contains(m.LeagueId))
                .Select(m => new { m.Id, m.ExternalMatchId, m.MatchDate })
                .ToListAsync(ct).ConfigureAwait(false);

            // Gün ataması SQL'de değil bellekte yapılır: takvim günü saat dilimi kuralıdır
            // ve tek merkezde (IstanbulCalendar) yaşamalıdır.
            var counts = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.ExternalMatchId)
                    ? $"id:{r.Id}"
                    : $"fx:{r.ExternalMatchId!.Trim()}")
                .Select(g => g.First())
                .GroupBy(r => IstanbulCalendar.TodayIn(r.MatchDate))
                .Where(g => g.Key >= oldest && g.Key <= today)
                .Select(g => new MatchResultDayDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    MatchCount = g.Count()
                })
                .OrderByDescending(d => d.Date, StringComparer.Ordinal)
                .ToList();

            return counts;
        }

        /// <summary>
        /// TAKIM ARAMASI — salt DB, sağlayıcıya SIFIR istek.
        ///
        /// KAPSAM SEZONDUR, GÜN DEĞİL: kullanıcı "Fenerbahçe" yazdığında seçili günle
        /// sınırlı kalmaz; mevcut sezonun tamamı taranır. Sezon penceresi
        /// <see cref="ILeagueSeasonResolver.SeasonYearOf"/> ile çözülür (Temmuz kırılımı,
        /// projenin mevcut sözleşmesi). Sezon çözülemezse SESSİZCE tüm tarihçe
        /// taranmaz — boş sonuç döner ve çağıran teşhis üretir.
        ///
        /// EŞLEŞME BELLEKTE YAPILIR (bkz. <see cref="TeamSearchTerm"/>): DB collation'ı
        /// Turkish_CI_AS, yani aksan DUYARLI; "fenerbahce" yazan kullanıcı SQL LIKE ile
        /// "Fenerbahçe"yi bulamazdı. Takım adı dizini kısa süreli önbellekte tutulur
        /// (9.050 satır), böylece her tuş vuruşunda tam tablo taranmaz.
        /// </summary>
        public async Task<List<MatchResultItemDto>> SearchByTeamAsync(
            string term, string scope, int maxResults = 100, CancellationToken ct = default)
        {
            var normalized = TeamSearchTerm.Normalize(term);
            // İKİ KARAKTERDEN KISA SORGU DB'YE HİÇ GİTMEZ.
            if (normalized.Length < TeamSearchTerm.MinLength) return new List<MatchResultItemDto>();

            var finished = string.Equals(scope, "finished", StringComparison.OrdinalIgnoreCase);
            var status = finished ? MatchStatuses.Finished : MatchStatuses.NotStarted;

            var nowUtc = DateTime.UtcNow;
            var seasonYear = _seasonResolver.SeasonYearOf(nowUtc);
            // Sezon penceresi: 1 Temmuz → ertesi 1 Temmuz (hariç).
            var seasonStart = new DateTime(seasonYear, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            var seasonEnd = seasonStart.AddYears(1);

            var teamIds = await MatchingTeamIdsAsync(normalized, ct).ConfigureAwait(false);
            if (teamIds.Count == 0) return new List<MatchResultItemDto>();

            var locked = LockedCompetitions.All.ToList();

            var query = _db.Matches.AsNoTracking()
                .Where(m => m.Status == status
                         && m.MatchDate >= seasonStart && m.MatchDate < seasonEnd
                         && locked.Contains(m.LeagueId)
                         && (teamIds.Contains(m.HomeTeamId) || teamIds.Contains(m.AwayTeamId)));

            // YAKLAŞAN yalnız GELECEĞİ gösterir: kickoff'u geçmiş ama durumu güncellenmemiş
            // bayat "başlamamış" kaydı listeye almaz.
            if (!finished) query = query.Where(m => m.MatchDate >= nowUtc);

            var rows = await (
                from m in query
                join ht in _db.Teams.AsNoTracking() on m.HomeTeamId equals ht.Id into htj
                from home in htj.DefaultIfEmpty()
                join at in _db.Teams.AsNoTracking() on m.AwayTeamId equals at.Id into atj
                from away in atj.DefaultIfEmpty()
                select new
                {
                    m.Id, m.ExternalMatchId, m.LeagueId, m.League, m.Round, m.MatchDate,
                    m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore,
                    m.HalfTimeHomeScore, m.HalfTimeAwayScore, m.Status,
                    HomeName = home != null ? home.Name : null,
                    HomeLogo = home != null ? home.LogoUrl : null,
                    AwayName = away != null ? away.Name : null,
                    AwayLogo = away != null ? away.LogoUrl : null
                }).ToListAsync(ct).ConfigureAwait(false);

            if (rows.Count == 0) return new List<MatchResultItemDto>();

            var deduped = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.ExternalMatchId)
                    ? $"id:{r.Id}"
                    : $"fx:{r.ExternalMatchId!.Trim()}")
                .Select(g => g.OrderBy(x => x.Id).First());

            // SIRA SÖZLEŞMESİ — arayüz bu sırayı YENİDEN ÜRETMEZ, korur.
            //  • finished : MatchDate AZALAN, eşitlikte MatchId AZALAN (en yeni en üstte)
            //  • upcoming : MatchDate ARTAN,  eşitlikte MatchId ARTAN  (en yakın en üstte)
            //
            // İkincil anahtar bilerek birincil anahtarla AYNI yöndedir: aynı dakikada
            // başlayan iki maçın sırası, listenin okunuş yönüyle çelişmesin. Anahtar
            // deterministiktir — aynı sorgu her çağrıda AYNI sırayı döndürür.
            var ordered = finished
                ? deduped.OrderByDescending(r => r.MatchDate).ThenByDescending(r => r.Id)
                : deduped.OrderBy(r => r.MatchDate).ThenBy(r => r.Id);

            var page = ordered.Take(Math.Clamp(maxResults, 1, 100)).ToList();

            var matchIds = page.Select(r => r.Id).ToList();
            var videoRows = await _db.MatchVideos.AsNoTracking()
                .Where(v => matchIds.Contains(v.MatchId))
                .ToListAsync(ct).ConfigureAwait(false);
            var playableSet = new HashSet<int>(
                videoRows.Where(MatchVideoRules.IsPlayable).Select(v => v.MatchId).Distinct());

            return page.Select(r =>
            {
                var round = string.IsNullOrWhiteSpace(r.Round) ? null : r.Round!.Trim();
                return new MatchResultItemDto
                {
                    MatchId           = r.Id,
                    ExternalFixtureId = string.IsNullOrWhiteSpace(r.ExternalMatchId) ? null : r.ExternalMatchId,
                    LeagueId          = r.LeagueId,
                    LeagueName        = r.League ?? string.Empty,
                    Round             = round,
                    MatchTypeLabel    = MatchTypeLabelResolver.Resolve(round),
                    MatchDateUtc      = r.MatchDate,
                    HomeTeam = new MatchResultTeamDto
                    {
                        TeamId = r.HomeTeamId, Name = r.HomeName ?? string.Empty, LogoUrl = r.HomeLogo
                    },
                    AwayTeam = new MatchResultTeamDto
                    {
                        TeamId = r.AwayTeamId, Name = r.AwayName ?? string.Empty, LogoUrl = r.AwayLogo
                    },
                    HomeScore              = r.HomeScore,
                    AwayScore              = r.AwayScore,
                    HalfTimeHomeScore      = r.HalfTimeHomeScore,
                    HalfTimeAwayScore      = r.HalfTimeAwayScore,
                    Status                 = r.Status,
                    HasPlayableOfficialVideo = playableSet.Contains(r.Id)
                };
            }).ToList();
        }

        /// <summary>
        /// Terimle eşleşen takım kimlikleri. Ad dizini kısa süre önbelleklenir: arama
        /// debounce'lu olsa da her sorguda 9.000 satırlık tam tablo okumak gereksizdir.
        /// </summary>
        private async Task<List<int>> MatchingTeamIdsAsync(string normalizedTerm, CancellationToken ct)
        {
            const string cacheKey = "matches:team-name-index";
            if (!_cache.TryGetValue<List<(int Id, string Name)>>(cacheKey, out var index) || index == null)
            {
                index = (await _db.Teams.AsNoTracking()
                        .Select(t => new { t.Id, t.Name })
                        .ToListAsync(ct).ConfigureAwait(false))
                    .Select(t => (t.Id, Name: t.Name ?? string.Empty))
                    .ToList();
                _cache.Set(cacheKey, index, TimeSpan.FromMinutes(10));
            }

            return index
                .Where(t => TeamSearchTerm.Matches(t.Name, normalizedTerm))
                .Select(t => t.Id)
                .ToList();
        }
    }
}
