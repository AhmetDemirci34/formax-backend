using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Formax.Infrastructure.Repositories
{
    public class MatchReadRepository : IMatchReadRepository
    {
        private readonly FormaxDbContext _context;

        /// <summary>
        /// KİLİTLİ MÜSABAKA KAPSAMI (Coverage:LeagueAllowList) — okuma tarafının TEK otoritesi.
        /// Depoda kapsam dışı maçlar (geçmiş ingestion'lardan) DURUYOR; bu süzgeç onların
        /// listelere, Discovery/Radar/Hero/Feed'e, maç detayına ve AI zincirine ÇIKMASINI engeller.
        /// Liste boşsa kısıtlama yoktur (geri-uyum). Kapsam ADLA değil canonical lig id'siyle belirlenir.
        /// </summary>
        private readonly HashSet<int> _allowedLeagues;

        public MatchReadRepository(FormaxDbContext context, IConfiguration config)
        {
            _context = context;
            _allowedLeagues = CoveragePolicy.LeagueAllowList(config);
        }

        /// <summary>Kapsam süzgeci — tüm okuma yollarında aynı kural.</summary>
        private IQueryable<Match> InScope(IQueryable<Match> source)
            => _allowedLeagues.Count == 0
                ? source
                : source.Where(m => _allowedLeagues.Contains(m.LeagueId));

        // 🔥 GLOBAL QUERY (FULL INCLUDE)
        public IQueryable<Match> Query()
        {
            return InScope(_context.Matches
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .AsNoTracking());
        }

        // 🔥 FIXED — INCLUDE EKLENDİ
        public Match? GetById(int id)
        {
            return InScope(_context.Matches
                    .Include(x => x.HomeTeam)
                    .Include(x => x.AwayTeam)
                    .AsNoTracking())
                .FirstOrDefault(m => m.Id == id);
        }

        // 🔥 FIXED — INCLUDE EKLENDİ
        public List<Match> GetUpcomingMatches(DateTime from, DateTime to)
        {
            return InScope(_context.Matches
                    .Include(x => x.HomeTeam)
                    .Include(x => x.AwayTeam)
                    .AsNoTracking())
                .Where(m =>
                    m.MatchDate >= from &&
                    m.MatchDate <= to)
                .OrderBy(m => m.MatchDate)
                .ToList();
        }

        // 🔥 LIST API (DTO projection — DOĞRU)
        public async Task<IReadOnlyList<MatchListItemDto>> GetMatchListAsync()
        {
            var utcNow = DateTime.UtcNow;
            var liveThreshold = utcNow.AddMinutes(-105);
            var startDate = utcNow.AddDays(-30);
            var endDate = utcNow.AddDays(30);

            var query =
                from m in InScope(_context.Matches.AsNoTracking())

                where m.MatchDate >= startDate
                      && m.MatchDate <= endDate

                join ht in _context.Teams.AsNoTracking()
                    on m.HomeTeamId equals ht.Id into htj
                from home in htj.DefaultIfEmpty()

                join at in _context.Teams.AsNoTracking()
                    on m.AwayTeamId equals at.Id into atj
                from away in atj.DefaultIfEmpty()

                orderby m.MatchDate descending

                select new MatchListItemDto
                {
                    MatchId = m.Id,
                    HomeTeam = home != null ? home.Name : "Team A",
                    AwayTeam = away != null ? away.Name : "Team B",
                    HomeTeamLogoUrl = home != null ? home.LogoUrl : null,
                    AwayTeamLogoUrl = away != null ? away.LogoUrl : null,
                    League = string.IsNullOrWhiteSpace(m.League)
                        ? "superlig"
                        : m.League.ToLower(),
                    StartTime = m.MatchDate,

                    // Derive status from match date — no dependency on stale DB Status column
                    Status = m.MatchDate > utcNow
                        ? "Scheduled"
                        : m.MatchDate >= liveThreshold
                            ? "Live"
                            : "Finished",

                    Score = m.MatchDate <= utcNow
                        ? new ScoreDto
                        {
                            Home = m.HomeScore,
                            Away = m.AwayScore
                        }
                        : null,

                    Minute = null
                };

            var list = await query.Take(50).ToListAsync();

            return await FillLiveMinutesAsync(list);
        }

        /// <summary>
        /// Canlı dakika + canlı skor: MatchLiveStats tek gerçek kaynaktır
        /// (Match.MatchMinute boş gelir). Yalnız listedeki CANLI maçlar için tek
        /// toplu sorgu atılır; veri yoksa alan null kalır — uydurma değer yazılmaz.
        /// </summary>
        private async Task<List<MatchListItemDto>> FillLiveMinutesAsync(List<MatchListItemDto> list)
        {
            // Canlı VE biten maçlar için: Matches.HomeScore/AwayScore güncellenmiyor
            // (0-0 kalıyor); gerçek skor MatchLiveStats'tadır. Kayıt yoksa mevcut
            // değer korunur — uydurma skor yazılmaz.
            var ids = list
                .Where(x => x.Status == "Live" || x.Status == "Finished")
                .Select(x => x.MatchId)
                .ToList();
            if (ids.Count == 0) return list;

            var stats = await _context.MatchLiveStats
                .AsNoTracking()
                .Where(s => ids.Contains(s.MatchId))
                .Select(s => new { s.MatchId, s.Minute, s.HomeScore, s.AwayScore })
                .ToListAsync();

            var byMatch = stats
                .GroupBy(x => x.MatchId)
                .ToDictionary(g => g.Key, g => g.First());

            return list
                .Select(item =>
                {
                    if (!byMatch.TryGetValue(item.MatchId, out var s)) return item;

                    return new MatchListItemDto
                    {
                        MatchId = item.MatchId,
                        HomeTeam = item.HomeTeam,
                        AwayTeam = item.AwayTeam,
                        HomeTeamLogoUrl = item.HomeTeamLogoUrl,
                        AwayTeamLogoUrl = item.AwayTeamLogoUrl,
                        League = item.League,
                        StartTime = item.StartTime,
                        Status = item.Status,
                        // Dakika yalnız canlı maçta anlamlıdır.
                        Minute = item.Status == "Live" ? s.Minute : null,
                        Score = new ScoreDto { Home = s.HomeScore, Away = s.AwayScore }
                    };
                })
                .ToList();
        }

        // Maçlar EKRANI — "şimdi" merkezli pencere (biten + canlı + yaklaşan aynı listede).
        public async Task<IReadOnlyList<MatchListItemDto>> GetScreenMatchListAsync(int take = 250)
        {
            var utcNow = DateTime.UtcNow;
            var liveThreshold = utcNow.AddMinutes(-105);
            // Tek pencere + kontenjan yeterli DEĞİL: küresel olarak saatte onlarca maç
            // oynandığı için kontenjanın tamamı "biten" maçlarla dolar ve canlı/yaklaşan
            // maçlar listeye giremez. Bu yüzden liste iki parçadan kurulur:
            //   A) yakın geçmişteki BİTEN maçlar (sınırlı)
            //   B) CANLI + YAKLAŞAN maçlar (listenin ağırlığı)
            const int finishedQuota = 40;
            var finishedFrom = utcNow.AddHours(-8);
            var upcomingTo = utcNow.AddDays(3);

            IQueryable<MatchListItemDto> Project(IQueryable<Domain.Entities.Match> src) =>
                from m in src
                join ht in _context.Teams.AsNoTracking() on m.HomeTeamId equals ht.Id into htj
                from home in htj.DefaultIfEmpty()
                join at in _context.Teams.AsNoTracking() on m.AwayTeamId equals at.Id into atj
                from away in atj.DefaultIfEmpty()
                select new MatchListItemDto
                {
                    MatchId = m.Id,
                    HomeTeam = home != null ? home.Name : "Team A",
                    AwayTeam = away != null ? away.Name : "Team B",
                    HomeTeamLogoUrl = home != null ? home.LogoUrl : null,
                    AwayTeamLogoUrl = away != null ? away.LogoUrl : null,
                    League = string.IsNullOrWhiteSpace(m.League) ? "" : m.League,
                    StartTime = m.MatchDate,
                    Status = m.MatchDate > utcNow
                        ? "Scheduled"
                        : m.MatchDate >= liveThreshold
                            ? "Live"
                            : "Finished",
                    Score = m.MatchDate <= utcNow
                        ? new ScoreDto { Home = m.HomeScore, Away = m.AwayScore }
                        : null,
                    Minute = null
                };

            // A — yakın geçmişteki biten maçlar (en yeniden geriye, sınırlı kontenjan).
            var finished = await Project(
                    InScope(_context.Matches.AsNoTracking())
                        .Where(m => m.MatchDate >= finishedFrom && m.MatchDate < liveThreshold)
                        .OrderByDescending(m => m.MatchDate)
                        .Take(finishedQuota))
                .ToListAsync();

            // B — canlı + yaklaşan (kickoff'a göre artan).
            var upcoming = await Project(
                    InScope(_context.Matches.AsNoTracking())
                        .Where(m => m.MatchDate >= liveThreshold && m.MatchDate <= upcomingTo)
                        .OrderBy(m => m.MatchDate)
                        .Take(Math.Max(0, take - finishedQuota)))
                .ToListAsync();

            // Kronolojik akış: biten (eskiden yeniye) → canlı → yaklaşan.
            var list = finished
                .OrderBy(x => x.StartTime)
                .Concat(upcoming)
                .ToList();

            return await FillLiveMinutesAsync(list);
        }

        // Targeted query
        public async Task<IReadOnlyList<MatchListItemDto>> GetMatchListByIdsAsync(IEnumerable<int> ids)
        {
            var idSet = ids.ToHashSet();

            if (idSet.Count == 0)
                return Array.Empty<MatchListItemDto>();

            var utcNow = DateTime.UtcNow;
            var liveThreshold = utcNow.AddMinutes(-105);

            var query =
                from m in InScope(_context.Matches.AsNoTracking())

                where idSet.Contains(m.Id)

                join ht in _context.Teams.AsNoTracking()
                    on m.HomeTeamId equals ht.Id into htj
                from home in htj.DefaultIfEmpty()

                join at in _context.Teams.AsNoTracking()
                    on m.AwayTeamId equals at.Id into atj
                from away in atj.DefaultIfEmpty()

                orderby m.MatchDate descending

                select new MatchListItemDto
                {
                    MatchId = m.Id,
                    HomeTeam = home != null ? home.Name : "Team A",
                    AwayTeam = away != null ? away.Name : "Team B",
                    HomeTeamLogoUrl = home != null ? home.LogoUrl : null,
                    AwayTeamLogoUrl = away != null ? away.LogoUrl : null,
                    League = string.IsNullOrWhiteSpace(m.League)
                        ? "superlig"
                        : m.League.ToLower(),
                    StartTime = m.MatchDate,

                    Status = m.MatchDate > utcNow
                        ? "Scheduled"
                        : m.MatchDate >= liveThreshold
                            ? "Live"
                            : "Finished",

                    Score = m.MatchDate <= utcNow
                        ? new ScoreDto
                        {
                            Home = m.HomeScore,
                            Away = m.AwayScore
                        }
                        : null,

                    Minute = null
                };

            return await query.ToListAsync();
        }

        // 🔥 NEW — TEAM RECENT MATCHES
        public List<Match> GetRecentMatchesForTeam(int teamId, int count = 5, bool finishedOnly = false, int? leagueId = null)
        {
            var utcNow = DateTime.UtcNow;
            var liveThreshold = utcNow.AddMinutes(-105);

            // Form/son maçlar da kapsam içinden okunur: kapsam dışı bir turnuvanın maçı
            // kullanıcıya "son 5 maç" olarak gösterilmez.
            //
            // finishedOnly: tarih filtresi TEK BAŞINA yetmez — geçmiş tarihli olup da
            // oynanmamış (NotStarted), iptal (Cancelled) veya hâlâ süren (Live) kayıtlar
            // da geçiyordu. Bunların skoru 0-0 olduğu için kullanıcıya "beraberlik" diye
            // görünüyorlardı. Ölçüldü (kapsam içi, geçmiş tarihli): Finished 6343,
            // Live 20, NotStarted 9, Cancelled 1.
            return InScope(_context.Matches
                    .Include(x => x.HomeTeam)
                    .Include(x => x.AwayTeam)
                    .AsNoTracking())
                .Where(m =>
                    (m.HomeTeamId == teamId || m.AwayTeamId == teamId) &&
                    m.MatchDate < liveThreshold &&
                    (!finishedOnly || m.Status == MatchStatuses.Finished) &&
                    // leagueId verildiyse yalnız o turnuva: "ligde son 5 maç" gerçekten
                    // lig maçı olsun; kupa/Avrupa maçı listeye karışmasın.
                    (leagueId == null || m.LeagueId == leagueId)
                )
                .OrderByDescending(m => m.MatchDate)
                .Take(count)
                .ToList();
        }

        // 🔥 NEW — HEAD TO HEAD
        public List<Match> GetHeadToHeadMatches(
            int homeTeamId,
            int awayTeamId,
            int count = 5,
            bool finishedOnly = false,
            int? excludeMatchId = null)
        {
            var utcNow = DateTime.UtcNow;
            var liveThreshold = utcNow.AddMinutes(-105);

            return InScope(_context.Matches
                    .Include(x => x.HomeTeam)
                    .Include(x => x.AwayTeam)
                    .AsNoTracking())
                .Where(m =>
                    (
                        m.HomeTeamId == homeTeamId &&
                        m.AwayTeamId == awayTeamId
                    ) ||
                    (
                        m.HomeTeamId == awayTeamId &&
                        m.AwayTeamId == homeTeamId
                    )
                )
                .Where(m => m.MatchDate < liveThreshold)
                // Oynanmamış/canlı/iptal karşılaşma H2H sayılmaz: skoru 0-0 olduğu için
                // "beraberlik" gibi görünüyordu. Maç 15497 kendi H2H'ında canlı hâliyle
                // 0-0 olarak listeleniyordu (ölçüldü).
                .Where(m => !finishedOnly || m.Status == MatchStatuses.Finished)
                // Görüntülenen maçın KENDİSİ geçmiş karşılaşma değildir.
                .Where(m => excludeMatchId == null || m.Id != excludeMatchId)
                .OrderByDescending(m => m.MatchDate)
                .Take(count)
                .ToList();
        }

        // ── SEZON KAPSAMI (30.08.2026 kök neden düzeltmesi) ─────────────────────
        //
        // ÖNCEKİ HATA: form listesi lig + tamamlanmışlık süzüyor, SEZON süzmüyordu.
        // Ölçüldü (Barcelona, LeagueId=140): en yeni 5 tamamlanmış lig maçının 4'ü
        // 2025/26 sezonundandı (10–23 Mayıs 2026) ve ekranda "bu sezonun formu" gibi
        // anlatılıyordu. Sezon penceresi artık sorgunun kendisindedir.

        public List<Match> GetSeasonLeagueMatchesForTeam(
            int teamId,
            int leagueId,
            DateTime seasonStartUtc,
            DateTime beforeUtc,
            int max = 20)
        {
            return InScope(_context.Matches
                    .Include(x => x.HomeTeam)
                    .Include(x => x.AwayTeam)
                    .AsNoTracking())
                .Where(m =>
                    (m.HomeTeamId == teamId || m.AwayTeamId == teamId) &&
                    m.LeagueId == leagueId &&
                    m.MatchDate >= seasonStartUtc &&
                    m.MatchDate < beforeUtc &&
                    m.Status == MatchStatuses.Finished)
                .OrderByDescending(m => m.MatchDate)
                .Take(max)
                .ToList();
        }

        public List<Match> GetSeasonLeagueFixturesBefore(
            int leagueId,
            DateTime seasonStartUtc,
            DateTime seasonEndUtc,
            DateTime kickoffBeforeUtc)
        {
            // Durum SÜZÜLMEZ: beklenen ile kesinleşeni karşılaştırmak için hepsi gerekir.
            return InScope(_context.Matches.AsNoTracking())
                .Where(m =>
                    m.LeagueId == leagueId &&
                    m.MatchDate >= seasonStartUtc &&
                    m.MatchDate < seasonEndUtc &&
                    m.MatchDate < kickoffBeforeUtc)
                .OrderBy(m => m.MatchDate)
                .ToList();
        }

        public List<Match> GetSettledLeagueMatchesInSeason(int leagueId, DateTime seasonStartUtc, DateTime seasonEndUtc)
        {
            // Puan durumu projeksiyonunun girdisi. Takım adları tablo satırları için gerekir.
            return InScope(_context.Matches
                    .Include(x => x.HomeTeam)
                    .Include(x => x.AwayTeam)
                    .AsNoTracking())
                .Where(m =>
                    m.LeagueId == leagueId &&
                    m.MatchDate >= seasonStartUtc &&
                    m.MatchDate < seasonEndUtc &&
                    m.Status == MatchStatuses.Finished)
                .OrderBy(m => m.MatchDate)
                .ToList();
        }
    }
}
