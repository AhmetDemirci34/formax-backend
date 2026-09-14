using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Sapma
{
    public sealed class GucSkoruCalculator : IGucSkoruCalculator
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly ITeamReadRepository _teamReadRepository;

        private const int DirectionThreshold = 8;
        private const int LastNOverall = 10;
        private const int LastNVenue = 6;
        private const int LeagueSampleMatches = 200;

        // ── İSTEK-İÇİ MEMOIZASYON (perf; sonuç DEĞİŞMEZ) ──────────────────────
        // Bu servis Scoped'tır → alanlar istek başına sıfırlanır, istekler arası sızma yok.
        // Maçlar ekranı 250 maçı tek istekte puanlıyordu ve her maç için:
        //   • lig baseline'ı (200 satırlık tarama) yeniden hesaplanıyor,
        //   • aynı takımın form sorguları defalarca tekrarlanıyordu.
        // Girdiler istek boyunca sabit olduğundan (aynı DB anlık görüntüsü) sonuç birebir
        // aynıdır; yalnız tekrar eden sorgular elenir.
        private LeagueBaseline? _leagueBaselineCache;
        private readonly Dictionary<(int TeamId, bool IsHomeVenue), int> _strengthCache = new();
        private readonly Dictionary<int, Team?> _teamCache = new();

        public GucSkoruCalculator(
            IMatchReadRepository matchReadRepository,
            ITeamReadRepository teamReadRepository)
        {
            _matchReadRepository = matchReadRepository;
            _teamReadRepository = teamReadRepository;
        }

        private Team? GetTeamCached(int teamId)
        {
            if (_teamCache.TryGetValue(teamId, out var cached)) return cached;
            var team = _teamReadRepository.GetById(teamId);
            _teamCache[teamId] = team;
            return team;
        }

        public GucSkoruResult CalculateForMatch(int matchId)
        {
            var match = _matchReadRepository.GetById(matchId);

            if (match == null)
            {
                return new GucSkoruResult
                {
                    GucSkoru = 50,
                    GercekGucYonu = "Denge",
                    HomeStrength = 50,
                    AwayStrength = 50
                };
            }

            var homeTeam = GetTeamCached(match.HomeTeamId);
            var awayTeam = GetTeamCached(match.AwayTeamId);

            var league = _leagueBaselineCache ??= ComputeLeagueBaseline();

            var homeStrength = GetTeamStrengthCached(match.HomeTeamId, true, homeTeam, league);
            var awayStrength = GetTeamStrengthCached(match.AwayTeamId, false, awayTeam, league);

            var delta = homeStrength - awayStrength;

            var dir = "Denge";
            if (Math.Abs(delta) >= DirectionThreshold)
                dir = delta > 0 ? "Home" : "Away";

            var matchGuc = Clamp01to100(50 + (int)Math.Round(delta / 2.0));

            return new GucSkoruResult
            {
                GucSkoru = matchGuc,
                GercekGucYonu = dir,
                HomeStrength = homeStrength,
                AwayStrength = awayStrength
            };
        }

        private int GetTeamStrengthCached(int teamId, bool isHomeVenue, Team? team, LeagueBaseline league)
        {
            var key = (teamId, isHomeVenue);
            if (_strengthCache.TryGetValue(key, out var cached)) return cached;
            var value = ComputeTeamStrengthDataDriven(teamId, isHomeVenue, team, league);
            _strengthCache[key] = value;
            return value;
        }

        private int ComputeTeamStrengthDataDriven(int teamId, bool isHomeVenue, Team? team, LeagueBaseline league)
        {
            var lastOverall = _matchReadRepository.Query()
                .Where(m => m.Status == "Finished" && (m.HomeTeamId == teamId || m.AwayTeamId == teamId))
                .OrderByDescending(m => m.MatchDate)
                .Take(LastNOverall)
                .ToList();

            var lastVenue = _matchReadRepository.Query()
                .Where(m => m.Status == "Finished" &&
                            (isHomeVenue ? m.HomeTeamId == teamId : m.AwayTeamId == teamId))
                .OrderByDescending(m => m.MatchDate)
                .Take(LastNVenue)
                .ToList();

            if (lastOverall.Count == 0)
                return ComputeFallbackStrength(team, league);

            var overallStats = WeightedStats(lastOverall, teamId);
            var venueStats = lastVenue.Count == 0 ? null : WeightedStats(lastVenue, teamId);

            var formComp = NormalizeCentered(overallStats.PointRate, 0.50, 0.50);

            var gdComp = ClampToUnit(overallStats.GoalDiffPerMatch / 2.0);

            var attackComp = NormalizeRelativeToBaseline(
                overallStats.GoalsForPerMatch,
                league.AvgGoalsForPerTeamPerMatch,
                0.50);

            var defenseComp = NormalizeRelativeToBaseline(
                league.AvgGoalsForPerTeamPerMatch - overallStats.GoalsAgainstPerMatch,
                league.AvgGoalsForPerTeamPerMatch,
                0.50);

            var venueComp = 0.0;
            if (venueStats != null)
            {
                venueComp = NormalizeCentered(venueStats.PointRate, overallStats.PointRate, 0.40);
                venueComp = ClampToUnit(venueComp);
            }

            double rankComp = 0.0;
            var rank = team?.LeagueRank ?? 0;
            if (rank > 0)
            {
                rankComp = 1.0 - ((rank - 1) / 19.0) * 2.0;
                rankComp = ClampToUnit(rankComp);
            }

            var stabilityComp = (team?.IsStableTeam ?? false) ? 0.25 : 0.0;

            var raw =
                50
                + (18 * formComp)
                + (14 * gdComp)
                + (10 * attackComp)
                + (10 * defenseComp)
                + (6 * venueComp)
                + (6 * rankComp)
                + (2 * stabilityComp);

            if (team != null)
            {
                var avgFor = team.AvgGoalsFor ?? 0;
                var avgAgainst = team.AvgGoalsAgainst ?? 0;

                var avgDiff = avgFor - avgAgainst;
                var avgComp = ClampToUnit(avgDiff / 2.0);

                raw += (4 * avgComp);
            }

            return Clamp01to100((int)Math.Round(raw));
        }

        private int ComputeFallbackStrength(Team? team, LeagueBaseline league)
        {
            if (team == null)
                return 50;

            var avgFor = team.AvgGoalsFor ?? 0;
            var avgAgainst = team.AvgGoalsAgainst ?? 0;

            var diff = avgFor - avgAgainst;
            var diffComp = ClampToUnit(diff / 2.0);

            var rank = team.LeagueRank ?? 0;
            var rankComp = 0.0;

            if (rank > 0)
            {
                rankComp = 1.0 - ((rank - 1) / 19.0) * 2.0;
                rankComp = ClampToUnit(rankComp);
            }

            var isStable = team.IsStableTeam ?? false;

            var raw = 50 + (12 * diffComp) + (10 * rankComp) + (isStable ? 2 : 0);

            return Clamp01to100((int)Math.Round(raw));
        }

        /// <summary>
        /// LİG ORTALAMASI ÖRNEKLEMİ — yalnız iki skor kolonu okunur.
        ///
        /// ÖLÇÜLDÜ (13.09.2026, SQL Server Express): sorgu eskiden bütün Match satırını ve iki
        /// takım JOIN'ini taşıyordu. Matches'teki 7 nvarchar(max) kolonu sıralama tahminini
        /// şişirdiği için her çalıştırma 214–252 MB bellek izni istiyor, 2 MB kullanıyordu.
        /// Aynı anda ikinci bir çağrı (maç detayı + SapmaSnapshotJob) RESOURCE_SEMAPHORE
        /// kuyruğunda 30 sn bekleyip "Execution Timeout Expired" ile düşüyordu; tekrar
        /// isteği kuyruk boşaldığı için 0,2 sn'de açılıyordu. Projeksiyonla izin 1,3 MB'dır.
        /// </summary>
        public static IQueryable<LeagueScoreSample> LeagueBaselineSampleQuery(IQueryable<Match> matches)
            => matches
                .Where(m => m.Status == "Finished")
                .OrderByDescending(m => m.MatchDate)
                .Take(LeagueSampleMatches)
                .Select(m => new LeagueScoreSample { HomeScore = m.HomeScore, AwayScore = m.AwayScore });

        private LeagueBaseline ComputeLeagueBaseline()
        {
            var sample = LeagueBaselineSampleQuery(_matchReadRepository.Query()).ToList();

            if (sample.Count == 0)
                return new LeagueBaseline { AvgGoalsForPerTeamPerMatch = 1.2 };

            var totalGoals = sample.Sum(m => m.HomeScore + m.AwayScore);
            var avg = totalGoals / (sample.Count * 2.0);

            avg = Math.Max(0.6, Math.Min(2.5, avg));

            return new LeagueBaseline { AvgGoalsForPerTeamPerMatch = avg };
        }

        private static WeightedTeamStats WeightedStats(List<Match> matches, int teamId)
        {
            double w = 1.0;
            const double decay = 0.85;

            double wSum = 0, points = 0, gf = 0, ga = 0;

            foreach (var m in matches)
            {
                var isHome = m.HomeTeamId == teamId;

                var scored = isHome ? m.HomeScore : m.AwayScore;
                var conceded = isHome ? m.AwayScore : m.HomeScore;

                var p = scored > conceded ? 3 : (scored == conceded ? 1 : 0);

                points += p * w;
                gf += scored * w;
                ga += conceded * w;

                wSum += w;
                w *= decay;
            }

            if (wSum <= 0.0001)
            {
                return new WeightedTeamStats
                {
                    PointRate = 0.5,
                    GoalsForPerMatch = 1,
                    GoalsAgainstPerMatch = 1,
                    GoalDiffPerMatch = 0
                };
            }

            return new WeightedTeamStats
            {
                PointRate = Math.Max(0, Math.Min(1, points / (wSum * 3.0))),
                GoalsForPerMatch = gf / wSum,
                GoalsAgainstPerMatch = ga / wSum,
                GoalDiffPerMatch = (gf - ga) / wSum
            };
        }

        private static double NormalizeCentered(double v, double c, double r)
            => ClampToUnit((v - c) / r);

        private static double NormalizeRelativeToBaseline(double v, double b, double clamp)
        {
            if (b <= 0) return 0;
            var rel = (v - b) / b;
            rel = Math.Max(-clamp, Math.Min(clamp, rel));
            return rel / clamp;
        }

        private static double ClampToUnit(double x) => Math.Max(-1, Math.Min(1, x));
        private static int Clamp01to100(int v) => Math.Max(0, Math.Min(100, v));

        private sealed class WeightedTeamStats
        {
            public double PointRate { get; set; }
            public double GoalsForPerMatch { get; set; }
            public double GoalsAgainstPerMatch { get; set; }
            public double GoalDiffPerMatch { get; set; }
        }

        private sealed class LeagueBaseline
        {
            public double AvgGoalsForPerTeamPerMatch { get; set; }
        }

        /// <summary>Lig ortalaması için okunan tek satır: yalnız skorlar.</summary>
        public sealed class LeagueScoreSample
        {
            public int HomeScore { get; set; }
            public int AwayScore { get; set; }
        }
    }
}