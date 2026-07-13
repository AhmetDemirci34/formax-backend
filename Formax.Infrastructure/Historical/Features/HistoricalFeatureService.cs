using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using TM = Formax.Infrastructure.Historical.Features.FeatureCalculators.TeamMatch;

namespace Formax.Infrastructure.Historical.Features;

/// <summary>
/// Historical Database'i SALT-OKUNUR kullanarak tek bir maç için özellik vektörü üretir (on-demand).
/// Feature matematiği <see cref="FeatureCalculators"/> ile paylaşılır (batch builder ile AYNI mantık).
/// Leakage-free: yalnız hedef maç tarihinden ÖNCEki maçlar + maç-öncesi Elo sütunu.
/// </summary>
public sealed class HistoricalFeatureService : IHistoricalFeatureService
{
    private const int HistoryCap = 60;

    private readonly FormaxDbContext _db;

    public HistoricalFeatureService(FormaxDbContext db) => _db = db;

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

        var hs = standings.TryGetValue(m.HomeTeamId, out var sh) ? sh : (points: 0, rank: (int?)null);
        var as_ = standings.TryGetValue(m.AwayTeamId, out var sa) ? sa : (points: 0, rank: (int?)null);

        var home = FeatureCalculators.BuildTeamFeatures(priorHome, m.HomeElo, m.MatchDate, hs.points, hs.rank);
        var away = FeatureCalculators.BuildTeamFeatures(priorAway, m.AwayElo, m.MatchDate, as_.points, as_.rank);

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

    private async Task<List<TM>> LoadPriorAsync(int teamId, DateTime before, CancellationToken ct)
    {
        var rows = await _db.HistoricalMatches.AsNoTracking()
            .Where(x => (x.HomeTeamId == teamId || x.AwayTeamId == teamId)
                        && x.MatchDate < before && x.FTHome != null && x.FTAway != null)
            .OrderByDescending(x => x.MatchDate).ThenByDescending(x => x.Id)
            .Take(HistoryCap)
            .Select(x => new { x.HomeTeamId, x.AwayTeamId, x.MatchDate, x.FTHome, x.FTAway, x.HomeShots, x.AwayShots, x.HomeTarget, x.AwayTarget })
            .ToListAsync(ct).ConfigureAwait(false);

        return rows.Select(x =>
        {
            var isHome = x.HomeTeamId == teamId;
            return new TM(x.MatchDate, isHome,
                isHome ? x.FTHome!.Value : x.FTAway!.Value,
                isHome ? x.FTAway!.Value : x.FTHome!.Value,
                isHome ? x.HomeShots : x.AwayShots,
                isHome ? x.HomeTarget : x.AwayTarget,
                isHome ? x.AwayShots : x.HomeShots,
                isHome ? x.AwayTeamId : x.HomeTeamId);
        }).ToList();
    }

    private async Task<(double, double, int)> HeadToHeadAsync(int home, int away, DateTime before, CancellationToken ct)
    {
        var rows = await _db.HistoricalMatches.AsNoTracking()
            .Where(x => x.MatchDate < before && x.FTHome != null && x.FTAway != null
                        && ((x.HomeTeamId == home && x.AwayTeamId == away) || (x.HomeTeamId == away && x.AwayTeamId == home)))
            .Select(x => new { x.HomeTeamId, x.FTHome, x.FTAway })
            .ToListAsync(ct).ConfigureAwait(false);
        if (rows.Count == 0) return (0d, 0d, 0);

        var homeWins = rows.Count(x =>
        {
            var homeTeamGf = x.HomeTeamId == home ? x.FTHome!.Value : x.FTAway!.Value;
            var homeTeamGa = x.HomeTeamId == home ? x.FTAway!.Value : x.FTHome!.Value;
            return homeTeamGf > homeTeamGa;
        });
        var goalsAvg = rows.Average(x => x.FTHome!.Value + x.FTAway!.Value);
        return (homeWins / (double)rows.Count, goalsAvg, rows.Count);
    }

    private async Task<Dictionary<int, (int points, int? rank)>> SeasonStandingsAsync(int compId, DateTime before, CancellationToken ct)
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

        var ranked = pts.Keys.OrderByDescending(t => pts[t]).ThenByDescending(t => gd.GetValueOrDefault(t)).ThenBy(t => t).ToList();
        var result = new Dictionary<int, (int, int?)>();
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
}
