using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// FORMAX — Takım karşılaştırma + H2H hazırlığının TEK kaynağı.
    ///
    /// NEDEN VAR: Aynı hesap daha önce yalnızca <c>GetMatchDetailAIContextUseCase</c> içinde vardı;
    /// diğer AI yüzeyleri (Discover / Decision / Voice / Narrative / Signals) boş DTO geçtiği için
    /// aynı maç farklı yüzeylerde farklı (ve zayıf) veriyle değerlendiriliyordu. Bu sınıf o mantığı
    /// tek yere alır → bütün yüzeyler AYNI gerçek veriyi görür.
    ///
    /// FORMÜL DEĞİŞMEDİ: son 10 maç, W=3/D=1 form skoru, gol ortalamaları, gol atma/clean sheet
    /// yüzdeleri ve son 10 karşılaşmadan H2H — hepsi kaynak use-case'ten BİREBİR taşındı.
    /// Yeni veri kaynağı, yeni API çağrısı, yeni job YOKTUR: yalnız mevcut Matches tablosu okunur.
    ///
    /// MEMOİZASYON: Scoped kayıtlıdır; aynı istek içinde aynı takım/eşleşme için tekrar DB'ye
    /// gidilmez (Discover tek istekte yüzlerce aday değerlendirir).
    /// </summary>
    public sealed class MatchComparisonFactory
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly ITeamReadRepository _teamReadRepository;

        private readonly Dictionary<(int TeamId, bool HomeOnly, bool AwayOnly), TeamComparisonDto> _comparisonCache = new();
        private readonly Dictionary<(int Home, int Away, int? Exclude), H2HDto> _h2hCache = new();

        public MatchComparisonFactory(
            IMatchReadRepository matchReadRepository,
            ITeamReadRepository teamReadRepository)
        {
            _matchReadRepository = matchReadRepository;
            _teamReadRepository = teamReadRepository;
        }

        /// <summary>İstek-içi cache istatistiği (doğrulama/teşhis içindir; davranışı etkilemez).</summary>
        public (int Comparisons, int H2Hs) CachedEntryCount => (_comparisonCache.Count, _h2hCache.Count);

        /// <summary>
        /// Takımın son 10 maçından karşılaştırma sinyalleri. Maç yoksa boş DTO döner (uydurma değer YOK).
        /// </summary>
        public TeamComparisonDto BuildTeamComparison(int teamId, bool homeOnly = false, bool awayOnly = false)
        {
            var key = (teamId, homeOnly, awayOnly);
            if (_comparisonCache.TryGetValue(key, out var cached))
                return cached;

            var result = ComputeTeamComparison(teamId, homeOnly, awayOnly);
            _comparisonCache[key] = result;
            return result;
        }

        /// <summary>İki takımın son 10 karşılaşması. Karşılaşma yoksa TotalMatches=0 döner.</summary>
        public H2HDto BuildH2H(int homeTeamId, int awayTeamId, int? excludeMatchId = null)
        {
            var key = (homeTeamId, awayTeamId, excludeMatchId);
            if (_h2hCache.TryGetValue(key, out var cached))
                return cached;

            var result = ComputeH2H(homeTeamId, awayTeamId, excludeMatchId);
            _h2hCache[key] = result;
            return result;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Hesaplar — GetMatchDetailAIContextUseCase'ten BİREBİR taşındı.
        // ────────────────────────────────────────────────────────────────────────

        private TeamComparisonDto ComputeTeamComparison(int teamId, bool homeOnly, bool awayOnly)
        {
            // YALNIZ OYNANMIŞ MAÇ. Tarih filtresi tek başına yetmez: geçmiş tarihli olup da
            // oynanmamış (NotStarted), iptal (Cancelled) veya hâlâ süren (Live) kayıtlar da
            // geçiyordu ve skorları 0-0 olduğu için form/oran hesabına BERABERLİK, CLEAN SHEET
            // ve "gol atamadı" olarak giriyorlardı. Ölçüldü (18.08.2026, kapsam içi geçmiş
            // tarihli): Finished 6515, NotStarted 9, Live 2, Cancelled 1 — on ikisinin de skoru
            // 0-0 ve 24 takımın son-10 penceresinde birer tanesi vardı (FormScore +1,
            // CleanSheetRate +%10, GoalScoringRate −%10).
            //
            // Aynı düzeltme H2H tarafında (ComputeH2H) zaten yapılmıştı; bu yol atlanmıştı.
            var recentMatches = _matchReadRepository.GetRecentMatchesForTeam(teamId, 10, finishedOnly: true);
            if (!recentMatches.Any()) return new TeamComparisonDto();

            var filteredMatches = recentMatches;
            if (homeOnly)  filteredMatches = recentMatches.Where(x => x.HomeTeamId == teamId).ToList();
            if (awayOnly)  filteredMatches = recentMatches.Where(x => x.AwayTeamId == teamId).ToList();

            var totalMatches   = recentMatches.Count;
            var goalsFor       = 0;
            var goalsAgainst   = 0;
            var scoredMatches  = 0;
            var cleanSheets    = 0;
            var formScore      = 0;

            foreach (var m in recentMatches)
            {
                var isHome = m.HomeTeamId == teamId;
                var gf = isHome ? m.HomeScore : m.AwayScore;
                var ga = isHome ? m.AwayScore : m.HomeScore;
                goalsFor     += gf;
                goalsAgainst += ga;
                if (gf > 0) scoredMatches++;
                if (ga == 0) cleanSheets++;
                if (gf > ga) formScore += 3;
                else if (gf == ga) formScore += 1;
            }

            var usedMatches = (filteredMatches.Count == 0 ? recentMatches : filteredMatches);
            var filteredGoals = usedMatches.Select(m =>
                m.HomeTeamId == teamId ? m.HomeScore : m.AwayScore).ToList();

            var team = _teamReadRepository.GetById(teamId);

            return new TeamComparisonDto
            {
                AvgGoalsFor    = Math.Round((double)goalsFor / totalMatches, 1),
                AvgGoalsAgainst = Math.Round((double)goalsAgainst / totalMatches, 1),
                GoalScoringRate = (int)Math.Round((double)scoredMatches / totalMatches * 100),
                CleanSheetRate  = (int)Math.Round((double)cleanSheets  / totalMatches * 100),
                HomeAwayAvgGoals = filteredGoals.Any()
                    ? Math.Round(filteredGoals.Average(), 1) : 0,
                FormScore  = formScore,
                LeagueRank = team?.LeagueRank ?? 0,
                // Oranların kaç gerçek maçtan geldiği — tüketici "veri yok" ile "değer 0"u
                // ayırt edebilsin diye taşınır. (Kayıt yoksa yukarıda SampleCount=0 döner.)
                SampleCount = totalMatches,

                // ÖRNEKLEMİN YAŞI VE KAPSAMI. Sayı tek başına yeterli değildir: beş maç
                // 2024'ten geliyorsa o "güncel form" değildir ve örneklem yalnız Avrupa
                // kupalarındansa takımın ulusal ligi kapsam dışı demektir. Tüketiciler
                // (FormEvidencePolicy) bu iki alanı okuyup zamansal kesinliğe izin verir.
                NewestMatchUtc = recentMatches.Max(m => m.MatchDate),
                Competitions = recentMatches
                    .Select(m => (m.League ?? "").Trim())
                    .Where(l => l.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private H2HDto ComputeH2H(int homeTeamId, int awayTeamId, int? excludeMatchId = null)
        {
            // Yalnız oynanmış karşılaşmalar: aksi hâlde geçmiş tarihli ama oynanmamış/canlı
            // kayıtlar 0-0 skoruyla "beraberlik" olarak sayılıyordu (ölçüldü: 31 kayıt).
            var matches = _matchReadRepository.GetHeadToHeadMatches(homeTeamId, awayTeamId, 10, finishedOnly: true, excludeMatchId: excludeMatchId);

            int homeWins = 0, awayWins = 0, draws = 0;
            var matchDtos = new List<H2HMatchDto>();

            foreach (var m in matches)
            {
                if (m.HomeScore > m.AwayScore)
                {
                    if (m.HomeTeamId == homeTeamId) homeWins++;
                    else awayWins++;
                }
                else if (m.HomeScore < m.AwayScore)
                {
                    if (m.AwayTeamId == awayTeamId) awayWins++;
                    else homeWins++;
                }
                else draws++;

                matchDtos.Add(new H2HMatchDto
                {
                    MatchDate    = m.MatchDate.ToString("yyyy-MM-dd"),
                    HomeTeamName = m.HomeTeam?.Name ?? string.Empty,
                    AwayTeamName = m.AwayTeam?.Name ?? string.Empty,
                    HomeScore    = m.HomeScore,
                    AwayScore    = m.AwayScore,
                    // İlk yarı — maçın kendi yönünde, olduğu gibi taşınır.
                    HalfTimeHomeScore = m.HalfTimeHomeScore,
                    HalfTimeAwayScore = m.HalfTimeAwayScore,
                    Competition  = m.League
                });
            }

            return new H2HDto
            {
                TotalMatches = matchDtos.Count,
                HomeWins     = homeWins,
                AwayWins     = awayWins,
                Draws        = draws,
                Matches      = matchDtos,
                FetchedAt    = DateTime.UtcNow
            };
        }
    }
}
