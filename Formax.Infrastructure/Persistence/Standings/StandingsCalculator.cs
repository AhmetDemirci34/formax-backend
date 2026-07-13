using System.Collections.Generic;
using System.Linq;

namespace Formax.Infrastructure.Persistence.Standings;

/// <summary>Bir competition'daki bitmiş maçın sadeleştirilmiş girdisi (provider-bağımsız).</summary>
public sealed record StandingMatch
{
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }
    public required int HomeScore { get; init; }
    public required int AwayScore { get; init; }
}

/// <summary>Türetilen puan durumu satırı (competition-adı + takım-adı).</summary>
public sealed record StandingRow
{
    public required string TeamName { get; init; }
    public int Played { get; init; }
    public int Won { get; init; }
    public int Drawn { get; init; }
    public int Lost { get; init; }
    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }
    public int Points { get; init; }
    public int Rank { get; init; }
}

/// <summary>
/// Bitmiş maç listesinden puan durumunu TÜRETİR. Saf fonksiyon: DB/IO yok, tek merkezde test edilebilir.
/// Kaynak = canonical Match geçmişi (provider değil) → sağlayıcı-bağımsız. Galibiyet=3, beraberlik=1.
/// Sıralama: puan ↓, averaj ↓, atılan gol ↓, isim ↑.
/// </summary>
public static class StandingsCalculator
{
    public static IReadOnlyList<StandingRow> Compute(IReadOnlyList<StandingMatch> matches)
    {
        var acc = new Dictionary<string, int[]>(); // team -> [P,W,D,L,GF,GA,Pts]

        int[] Row(string t)
        {
            if (!acc.TryGetValue(t, out var r)) { r = new int[7]; acc[t] = r; }
            return r;
        }

        foreach (var m in matches)
        {
            if (string.IsNullOrWhiteSpace(m.HomeTeam) || string.IsNullOrWhiteSpace(m.AwayTeam))
                continue;

            var h = Row(m.HomeTeam);
            var a = Row(m.AwayTeam);

            h[0]++; a[0]++;                       // Played
            h[4] += m.HomeScore; h[5] += m.AwayScore; // GF/GA
            a[4] += m.AwayScore; a[5] += m.HomeScore;

            if (m.HomeScore > m.AwayScore) { h[1]++; a[3]++; h[6] += 3; }       // home win
            else if (m.HomeScore < m.AwayScore) { a[1]++; h[3]++; a[6] += 3; }  // away win
            else { h[2]++; a[2]++; h[6] += 1; a[6] += 1; }                      // draw
        }

        var ordered = acc
            .Select(kv => new { Team = kv.Key, R = kv.Value })
            .OrderByDescending(x => x.R[6])                     // Points
            .ThenByDescending(x => x.R[4] - x.R[5])             // Goal diff
            .ThenByDescending(x => x.R[4])                      // Goals for
            .ThenBy(x => x.Team, System.StringComparer.Ordinal)
            .ToList();

        var result = new List<StandingRow>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var r = ordered[i].R;
            result.Add(new StandingRow
            {
                TeamName = ordered[i].Team,
                Played = r[0], Won = r[1], Drawn = r[2], Lost = r[3],
                GoalsFor = r[4], GoalsAgainst = r[5], Points = r[6],
                Rank = i + 1
            });
        }
        return result;
    }
}
