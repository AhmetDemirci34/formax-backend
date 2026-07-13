using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Historical.Features;

/// <summary>
/// Historical Database'i SALT-OKUNUR kullanarak maç öncesi özellikler üretir.
/// Tüm özellikler deterministik + leakage-free: yalnızca hedef maçın TARİHİNDEN ÖNCEki maçlar
/// (ve maçın kendi maç-öncesi Elo sütunları) kullanılır; hedef maçın SONUCU asla kullanılmaz.
/// Her özellik bağımsız/saf metottur (tekrar kullanılabilir). Historical şeması DEĞİŞTİRİLMEZ.
/// </summary>
public sealed class HistoricalFeatureService : IHistoricalFeatureService
{
    private const int WindowRecent = 10; // rate/average pencereleri
    private const int HistoryCap = 60;   // takım başına çekilecek geçmiş maç üst sınırı

    private readonly FormaxDbContext _db;

    public HistoricalFeatureService(FormaxDbContext db) => _db = db;

    /// <summary>Bir takımın maçı, kendi perspektifine normalize edilmiş (en yeni önce).</summary>
    private readonly record struct TeamMatch(DateTime Date, bool IsHome, int Gf, int Ga, int? Shots, int? Target, int? OppShots)
    {
        public char Result => Gf > Ga ? 'W' : Gf == Ga ? 'D' : 'L';
    }

    public async Task<MatchFeatureVector?> ComputeAsync(int historicalMatchId, CancellationToken cancellationToken = default)
    {
        var m = await _db.HistoricalMatches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == historicalMatchId, cancellationToken).ConfigureAwait(false);
        if (m is null) return null;

        var priorHome = await LoadPriorAsync(m.HomeTeamId, m.MatchDate, cancellationToken).ConfigureAwait(false);
        var priorAway = await LoadPriorAsync(m.AwayTeamId, m.MatchDate, cancellationToken).ConfigureAwait(false);
        var (h2hWin, h2hGoals, h2hN) = await HeadToHeadAsync(m.HomeTeamId, m.AwayTeamId, m.MatchDate, cancellationToken).ConfigureAwait(false);
        var standings = await SeasonStandingsAsync(m.HistoricalCompetitionId, m.MatchDate, cancellationToken).ConfigureAwait(false);
        var leagueStrength = await LeagueStrengthAsync(m.HistoricalCompetitionId, m.MatchDate, cancellationToken).ConfigureAwait(false);

        var home = BuildTeamFeatures(priorHome, m.HomeElo, m.MatchDate, standings, m.HomeTeamId);
        var away = BuildTeamFeatures(priorAway, m.AwayElo, m.MatchDate, standings, m.AwayTeamId);

        return new MatchFeatureVector
        {
            MatchId = m.Id,
            Home = home,
            Away = away,
            EloDifference = (m.HomeElo is double he && m.AwayElo is double ae) ? he - ae : (double?)null,
            HomeAdvantage = home.HomeWinRate - away.AwayWinRate,
            HeadToHeadWinRate = h2hWin,
            HeadToHeadGoalsAverage = h2hGoals,
            HeadToHeadSampleSize = h2hN,
            LeagueStrength = leagueStrength
        };
    }

    // ── Veri yükleme (yalnız hedef tarihten ÖNCEsi) ──

    private async Task<List<TeamMatch>> LoadPriorAsync(int teamId, DateTime before, CancellationToken ct)
    {
        var rows = await _db.HistoricalMatches.AsNoTracking()
            .Where(x => (x.HomeTeamId == teamId || x.AwayTeamId == teamId)
                        && x.MatchDate < before && x.FTHome != null && x.FTAway != null)
            .OrderByDescending(x => x.MatchDate).ThenByDescending(x => x.Id)
            .Take(HistoryCap)
            .Select(x => new { x.HomeTeamId, x.MatchDate, x.FTHome, x.FTAway, x.HomeShots, x.AwayShots, x.HomeTarget, x.AwayTarget })
            .ToListAsync(ct).ConfigureAwait(false);

        return rows.Select(x =>
        {
            var isHome = x.HomeTeamId == teamId;
            return new TeamMatch(
                x.MatchDate, isHome,
                isHome ? x.FTHome!.Value : x.FTAway!.Value,
                isHome ? x.FTAway!.Value : x.FTHome!.Value,
                isHome ? x.HomeShots : x.AwayShots,
                isHome ? x.HomeTarget : x.AwayTarget,
                isHome ? x.AwayShots : x.HomeShots);
        }).ToList();
    }

    private async Task<(double winRate, double goalsAvg, int n)> HeadToHeadAsync(int home, int away, DateTime before, CancellationToken ct)
    {
        var rows = await _db.HistoricalMatches.AsNoTracking()
            .Where(x => x.MatchDate < before && x.FTHome != null && x.FTAway != null
                        && ((x.HomeTeamId == home && x.AwayTeamId == away) || (x.HomeTeamId == away && x.AwayTeamId == home)))
            .Select(x => new { x.HomeTeamId, x.FTHome, x.FTAway })
            .ToListAsync(ct).ConfigureAwait(false);
        if (rows.Count == 0) return (0d, 0d, 0);

        var homeWins = rows.Count(x =>
        {
            var hostGf = x.FTHome!.Value; var hostGa = x.FTAway!.Value;
            var homeTeamGf = x.HomeTeamId == home ? hostGf : hostGa;
            var homeTeamGa = x.HomeTeamId == home ? hostGa : hostGf;
            return homeTeamGf > homeTeamGa;
        });
        var goalsAvg = rows.Average(x => x.FTHome!.Value + x.FTAway!.Value);
        return (homeWins / (double)rows.Count, goalsAvg, rows.Count);
    }

    private async Task<Dictionary<int, (int points, int rank)>> SeasonStandingsAsync(int compId, DateTime before, CancellationToken ct)
    {
        var seasonStart = SeasonStart(before);
        var rows = await _db.HistoricalMatches.AsNoTracking()
            .Where(x => x.HistoricalCompetitionId == compId && x.MatchDate < before && x.MatchDate >= seasonStart
                        && x.FTHome != null && x.FTAway != null)
            .Select(x => new { x.HomeTeamId, x.AwayTeamId, x.FTHome, x.FTAway })
            .ToListAsync(ct).ConfigureAwait(false);

        var pts = new Dictionary<int, int>();
        var gd = new Dictionary<int, int>();
        void Add(int t, int p, int diff) { pts[t] = pts.GetValueOrDefault(t) + p; gd[t] = gd.GetValueOrDefault(t) + diff; }
        foreach (var x in rows)
        {
            int h = x.FTHome!.Value, a = x.FTAway!.Value;
            if (h > a) { Add(x.HomeTeamId, 3, h - a); Add(x.AwayTeamId, 0, a - h); }
            else if (h == a) { Add(x.HomeTeamId, 1, 0); Add(x.AwayTeamId, 1, 0); }
            else { Add(x.HomeTeamId, 0, h - a); Add(x.AwayTeamId, 3, a - h); }
        }

        var ranked = pts.Keys
            .OrderByDescending(t => pts[t]).ThenByDescending(t => gd.GetValueOrDefault(t)).ThenBy(t => t)
            .ToList();
        var result = new Dictionary<int, (int, int)>();
        for (var i = 0; i < ranked.Count; i++) result[ranked[i]] = (pts[ranked[i]], i + 1);
        return result;
    }

    private async Task<double> LeagueStrengthAsync(int compId, DateTime before, CancellationToken ct)
    {
        var seasonStart = SeasonStart(before);
        var elos = await _db.HistoricalMatches.AsNoTracking()
            .Where(x => x.HistoricalCompetitionId == compId && x.MatchDate < before && x.MatchDate >= seasonStart
                        && (x.HomeElo != null || x.AwayElo != null))
            .Select(x => new { x.HomeElo, x.AwayElo })
            .ToListAsync(ct).ConfigureAwait(false);
        var vals = elos.SelectMany(x => new[] { x.HomeElo, x.AwayElo }).Where(v => v != null).Select(v => v!.Value).ToList();
        return vals.Count == 0 ? 0d : vals.Average();
    }

    private static DateTime SeasonStart(DateTime d) => d.Month >= 7 ? new DateTime(d.Year, 7, 1) : new DateTime(d.Year - 1, 7, 1);

    // ── Özellik hesabı (bağımsız/saf) ──

    private static TeamFeatures BuildTeamFeatures(List<TeamMatch> prior, double? elo, DateTime matchDate, Dictionary<int, (int points, int rank)> standings, int teamId)
    {
        var recent = prior.Take(WindowRecent).ToList();
        var homeMatches = prior.Where(x => x.IsHome).Take(WindowRecent).ToList();
        var awayMatches = prior.Where(x => !x.IsHome).Take(WindowRecent).ToList();
        var standing = standings.TryGetValue(teamId, out var s) ? s : (points: 0, rank: (int?)null);

        return new TeamFeatures
        {
            Last3Form = Form(prior, 3),
            Last5Form = Form(prior, 5),
            Last10Form = Form(prior, 10),
            WinRate = Rate(recent, x => x.Result == 'W'),
            HomeWinRate = Rate(homeMatches, x => x.Result == 'W'),
            AwayWinRate = Rate(awayMatches, x => x.Result == 'W'),
            GoalsScoredAverage = Avg(recent, x => x.Gf),
            GoalsConcededAverage = Avg(recent, x => x.Ga),
            GoalDifference = Avg(recent, x => x.Gf - x.Ga),
            BTTSRate = Rate(recent, x => x.Gf > 0 && x.Ga > 0),
            Over25Rate = Rate(recent, x => x.Gf + x.Ga > 2),   // > 2.5 goller
            CleanSheetRate = Rate(recent, x => x.Ga == 0),
            FailedToScoreRate = Rate(recent, x => x.Gf == 0),
            WinningStreak = Streak(prior, x => x.Result == 'W'),
            LosingStreak = Streak(prior, x => x.Result == 'L'),
            UnbeatenStreak = Streak(prior, x => x.Result != 'L'),
            HomeGoalsAverage = Avg(homeMatches, x => x.Gf),
            AwayGoalsAverage = Avg(awayMatches, x => x.Gf),
            DaysSinceLastMatch = prior.Count > 0 ? (int?)(matchDate - prior[0].Date).Days : null,
            LeaguePosition = standing.rank,
            LeaguePoints = standing.points,
            ShotAccuracy = ShotAccuracy(recent),
            ShotDifference = ShotDifference(recent),
            OffensiveRating = OffensiveRating(recent),
            DefensiveRating = DefensiveRating(recent),
            RecentMomentum = RecentMomentum(prior),
            Elo = elo,
            SampleSize = prior.Count
        };
    }

    private static double Points(char r) => r == 'W' ? 3 : r == 'D' ? 1 : 0;

    private static double Form(List<TeamMatch> prior, int n)
    {
        var take = prior.Take(n).ToList();
        if (take.Count == 0) return 0d;
        return take.Sum(x => Points(x.Result)) / (take.Count * 3d); // [0,1]
    }

    private static double Rate(List<TeamMatch> list, Func<TeamMatch, bool> pred)
        => list.Count == 0 ? 0d : list.Count(pred) / (double)list.Count;

    private static double Avg(List<TeamMatch> list, Func<TeamMatch, int> sel)
        => list.Count == 0 ? 0d : list.Average(sel);

    private static int Streak(List<TeamMatch> prior, Func<TeamMatch, bool> keep)
    {
        var n = 0;
        foreach (var x in prior) { if (keep(x)) n++; else break; }
        return n;
    }

    private static double? ShotAccuracy(List<TeamMatch> list)
    {
        var with = list.Where(x => x.Shots is > 0 && x.Target != null).ToList();
        if (with.Count == 0) return null;
        double shots = with.Sum(x => x.Shots!.Value), target = with.Sum(x => x.Target!.Value);
        return shots <= 0 ? null : target / shots; // [0,1]
    }

    private static double? ShotDifference(List<TeamMatch> list)
    {
        var with = list.Where(x => x.Shots != null && x.OppShots != null).ToList();
        return with.Count == 0 ? null : with.Average(x => x.Shots!.Value - x.OppShots!.Value);
    }

    /// <summary>Deterministik ofansif skor [0,1]: 0.6·min(GS/3,1) + 0.25·Over2.5 + 0.15·(ShotAccuracy||0.5).</summary>
    private static double OffensiveRating(List<TeamMatch> recent)
    {
        if (recent.Count == 0) return 0d;
        var gs = Avg(recent, x => x.Gf);
        var over = Rate(recent, x => x.Gf + x.Ga > 2);
        var acc = ShotAccuracy(recent) ?? 0.5;
        return 0.6 * Math.Min(gs / 3d, 1d) + 0.25 * over + 0.15 * acc;
    }

    /// <summary>Deterministik defansif skor [0,1]: 0.5·CleanSheet + 0.5·1/(1+GA).</summary>
    private static double DefensiveRating(List<TeamMatch> recent)
    {
        if (recent.Count == 0) return 0d;
        var cs = Rate(recent, x => x.Ga == 0);
        var ga = Avg(recent, x => x.Ga);
        return 0.5 * cs + 0.5 * (1d / (1d + ga));
    }

    /// <summary>Üstel ağırlıklı son-5 form [0,1]: en yeni maç en yüksek ağırlık (5..1).</summary>
    private static double RecentMomentum(List<TeamMatch> prior)
    {
        var take = prior.Take(5).ToList();
        if (take.Count == 0) return 0d;
        double num = 0, den = 0;
        for (var i = 0; i < take.Count; i++)
        {
            var w = take.Count - i; // 5,4,3,2,1
            num += w * Points(take[i].Result);
            den += w * 3d;
        }
        return den == 0 ? 0d : num / den;
    }
}
