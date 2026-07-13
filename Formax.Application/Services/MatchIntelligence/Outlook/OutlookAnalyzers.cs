using Formax.Application.DTOs.Matches;

namespace Formax.Application.Services.MatchIntelligence.Outlook;

/// <summary>Son maç formu (Görev #016).</summary>
public static class FormAnalyzer
{
    public static List<string> Recent(List<LastMatchDto>? matches, int take = 5)
        => (matches ?? new()).Take(take).Select(m => m.Result).ToList();

    /// <summary>Son N maç puanı (W=3, D=1, L=0) — momentum için.</summary>
    public static int Points(List<LastMatchDto>? matches, int take = 5)
        => (matches ?? new()).Take(take).Sum(m => m.Result == "W" ? 3 : m.Result == "D" ? 1 : 0);
}

/// <summary>Takım performansı: FormScore'dan 0–100 skor + iç saha/deplasman etiketi.</summary>
public static class PerformanceAnalyzer
{
    public static int Score(TeamComparisonDto c)
        => Math.Clamp((int)Math.Round(c.FormScore / 30.0 * 100), 0, 100);

    public static string Label(TeamComparisonDto c, bool isHome)
    {
        var s = Score(c);
        var venue = isHome ? "İç sahada" : "Deplasmanda";
        var q = s >= 67 ? "güçlü" : s >= 40 ? "dengeli" : "zayıf";
        return $"{venue} {q}";
    }
}

/// <summary>Gol eğilimi: karşılıklı gol oranı + toplam gol beklentisi.</summary>
public static class GoalTrendAnalyzer
{
    public static string Trend(TeamComparisonDto home, TeamComparisonDto away, H2HDto? h2h)
    {
        var kg = Math.Clamp((home.GoalScoringRate + away.GoalScoringRate) / 2, 0, 100);
        var avgTotal = home.AvgGoalsFor + away.AvgGoalsFor;
        var over = avgTotal >= 2.5 ? " · Üst 2.5 eğilimi" : "";
        return $"KG %{kg}{over}";
    }
}

/// <summary>Maç temposu: toplam gol ortalamasından.</summary>
public static class TempoAnalyzer
{
    public static string Tempo(TeamComparisonDto home, TeamComparisonDto away)
    {
        var t = home.AvgGoalsFor + away.AvgGoalsFor;
        return t >= 2.7 ? "Yüksek" : t >= 2.0 ? "Orta" : "Düşük";
    }
}

/// <summary>Oyun dengesi: form + lig sırası farkından (0 ev ↔ 100 deplasman).</summary>
public static class BalanceAnalyzer
{
    public static int Balance(TeamComparisonDto home, TeamComparisonDto away)
    {
        var formGap = home.FormScore - away.FormScore; // + = ev daha güçlü
        var rankGap = 0;
        if (home.LeagueRank > 0 && away.LeagueRank > 0)
            rankGap = away.LeagueRank - home.LeagueRank; // + = ev daha iyi (düşük sıra)
        var lean = formGap * 2.5 + rankGap * 1.5;       // + = ev lehine
        return Math.Clamp((int)Math.Round(50 - lean), 0, 100);
    }
}

/// <summary>Momentum: son maç puanlarından hangi tarafın yükselişte olduğu.</summary>
public static class MomentumAnalyzer
{
    public static string Momentum(List<LastMatchDto>? homeLast, List<LastMatchDto>? awayLast)
    {
        var h = FormAnalyzer.Points(homeLast);
        var a = FormAnalyzer.Points(awayLast);
        var diff = h - a;
        return diff >= 3 ? "home" : diff <= -3 ? "away" : "balanced";
    }
}
