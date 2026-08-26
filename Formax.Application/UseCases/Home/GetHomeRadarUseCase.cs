using Formax.Application.Abstractions;
using Formax.Application.DTOs.Home;
using Formax.Application.DTOs.Recommendations;
using Formax.Application.Interfaces;
using Formax.Application.Services.Home;
using Formax.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Home
{
    public sealed class GetHomeRadarUseCase
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly IMatchSapmaSnapshotRepository _snapshotRepository;
        private readonly LeagueBaselineService _leagueBaselineService;
        private readonly IUserInterestService _userInterestService;
        private readonly IAppDbContext _db;

        public GetHomeRadarUseCase(
            IMatchReadRepository matchReadRepository,
            IMatchSapmaSnapshotRepository snapshotRepository,
            IUserInterestService userInterestService,
            LeagueBaselineService leagueBaselineService,
            IAppDbContext db)
        {
            _matchReadRepository = matchReadRepository;
            _snapshotRepository = snapshotRepository;
            _leagueBaselineService = leagueBaselineService;
            _userInterestService = userInterestService;
            _db = db;
        }

        public async Task<HomeRadarResponseDto> ExecuteAsync(int? userId, bool isPremium, DateTime utcNow)
        {
            var items = await _matchReadRepository.GetMatchListAsync();

            if (items == null || items.Count == 0)
            {
                return new HomeRadarResponseDto
                {
                    BannerText = "Radar boş",
                    ContentAccess = BuildContentAccess(isPremium),
                    Matches = Array.Empty<HomeRadarMatchDto>()
                };
            }

            var candidates = items
                .OrderByDescending(x => x.MatchId)
                .Take(20)
                .ToList();

            var snapshots = await _snapshotRepository.GetFreshByMatchIdsAsync(
                candidates.Select(x => x.MatchId).ToList(), utcNow);

            if (snapshots == null || snapshots.Count == 0)
            {
                snapshots = candidates
                    .Select(m => new MatchSapmaSnapshot
                    {
                        MatchId = m.MatchId,
                        Sapma = 10,
                        ComputedAtUtc = utcNow,
                        SessizMi = false
                    })
                    .ToList();
            }

            var snapMap = snapshots.ToDictionary(x => x.MatchId, x => x);
            var baselineMap = _leagueBaselineService.Build(candidates, snapshots, utcNow);

            // N+1 FIX (perf): trend istatistikleri döngü içinde maç-başına sorgulanıyordu
            // (20 aday = 20 ayrı senkron DB round-trip). Tek sorguda çekilir; okunan değerler
            // ve fallback davranışı (kayıt yoksa deterministik Random(matchId)) AYNI kalır.
            var candidateIds = candidates.Select(x => x.MatchId).ToList();
            var trendByMatch = _db.MatchTrendStats
                .Where(x => candidateIds.Contains(x.MatchId))
                .ToList()
                .GroupBy(x => x.MatchId)
                .ToDictionary(g => g.Key, g => g.First());

            var ranked = new List<HomeRadarMatchDto>();

            foreach (var match in candidates)
            {
                if (!snapMap.TryGetValue(match.MatchId, out var snapshot))
                    continue;

                var league = Normalize(match.League ?? "");

                // Şimdilik sabit (learning bağlanınca değişecek)
                var teamInterest = 50;
                var leagueInterest = 50;
                var behaviorMomentum = 50;

                var leagueBaseline = Get(baselineMap, league);
                var proximity = 50;

                var matchHeat = Clamp((int)Math.Round(
                    (snapshot.Sapma * 0.5) +
                    (leagueBaseline * 0.2) +
                    (proximity * 0.3)));

                var radarScore = Clamp((int)Math.Round(
                    (snapshot.Sapma * 0.3) +
                    (teamInterest * 0.3) +
                    (leagueInterest * 0.2) +
                    (behaviorMomentum * 0.2)));

                // 🔥 TREND (TYPE FIX) — batch map'ten okunur (yukarıda tek sorgu).
                trendByMatch.TryGetValue(match.MatchId, out var trend);

                int playRate = trend != null
                    ? (int)trend.PlayRate
                    : new Random(match.MatchId).Next(10, 90);

                int delta = trend != null
                    ? (int)trend.Delta
                    : new Random(match.MatchId + 99).Next(-10, 10);

                ranked.Add(new HomeRadarMatchDto
                {
                    MatchId = match.MatchId,

                    // ✅ STRING SAFE TEAM
                    Teams = new HomeTeamsDto
                    {
                        Home = !string.IsNullOrWhiteSpace(match.HomeTeam)
                            ? match.HomeTeam
                            : "HOME",

                        Away = !string.IsNullOrWhiteSpace(match.AwayTeam)
                            ? match.AwayTeam
                            : "AWAY"
                    },

                    League = match.League ?? "",
                    Sapma = snapshot.Sapma,

                    RadarScore = radarScore,

                    TeamInterestScore = teamInterest,
                    LeagueInterestScore = leagueInterest,
                    BehaviorMomentumScore = behaviorMomentum,
                    MatchHeatScore = matchHeat,

                    PlayRate = playRate,
                    TrendDelta = delta,

                    // 🔥 ENGINE İÇİN ZORUNLU
                    Trend = new TrendDto
                    {
                        PlayRate = playRate,
                        TrendDelta = delta,
                        LastUpdatedAt = DateTime.UtcNow
                    }
                });
            }

            return new HomeRadarResponseDto
            {
                BannerText = "Radar aktif",
                ContentAccess = BuildContentAccess(isPremium),

                Matches = ranked
                    .OrderByDescending(x => x.RadarScore)
                    .Take(20)
                    .ToList()
            };
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            return value.Trim().ToLowerInvariant();
        }

        private static int Get(IReadOnlyDictionary<string, int> map, string key)
            => map.TryGetValue(key, out var v) ? v : 0;

        private static int Clamp(int value)
            => Math.Max(0, Math.Min(100, value));

        private static HomeContentAccessDto BuildContentAccess(bool isPremium)
            => new() { IsPremiumUser = isPremium };
    }
}