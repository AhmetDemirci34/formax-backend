using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Outlook;

/// <summary>Hero Outlook + AI Summary üretir (Görev #016). LLM anlatısı varsa Hero'da kullanılır.</summary>
public static class OutlookSummaryGenerator
{
    public static string Hero(int balance, string tempo, string homeName, string awayName, string? narrative)
    {
        if (!string.IsNullOrWhiteSpace(narrative)) return narrative;

        var lean = balance < 42 ? $"Dar bir {homeName} üstünlüğü"
                 : balance > 58 ? $"Dar bir {awayName} üstünlüğü"
                 : "Dengeli bir görünüm";
        var tempoWord = tempo == "Yüksek" ? "yüksek tempolu ve karşılıklı gollü"
                      : tempo == "Orta" ? "dengeli tempolu"
                      : "temkinli";
        return $"{lean}; {tempoWord} bir maç bekleniyor.";
    }

    public static string Summary(
        int balance, string tempo, string momentum, string goalTrend,
        OutlookTeamDto home, OutlookTeamDto away)
    {
        var leanTeam = balance < 42 ? home.Team : balance > 58 ? away.Team : null;
        var momTeam = momentum == "home" ? home.Team : momentum == "away" ? away.Team : null;

        var s = leanTeam != null
            ? $"Genel görünüm dar bir {leanTeam} üstünlüğüne işaret ediyor"
            : "Genel görünüm dengeli";
        if (momTeam != null) s += $"; momentum {momTeam} tarafında";
        s += $". Tempo {tempo.ToLowerInvariant()}, {goalTrend.ToLowerInvariant()} eğilimi öne çıkıyor.";
        return s;
    }
}

/// <summary>Evidence: Form, Performans, Gol eğilimi, Tempo, Momentum (Görev #016).</summary>
public static class OutlookEvidenceBuilder
{
    public static List<OutlookEvidenceItemDto> Build(
        OutlookTeamDto home, OutlookTeamDto away, string goalTrend, string tempo, string momentum)
    {
        var mom = momentum == "home" ? home.Team : momentum == "away" ? away.Team : "Dengeli";
        return new()
        {
            new() { Label = "Son form",  Detail = $"{home.Team} {string.Join("", home.Form)} · {away.Team} {string.Join("", away.Form)}" },
            new() { Label = "İç saha performansı",  Detail = $"{home.Team} {home.PerformanceScore}/100" },
            new() { Label = "Deplasman performansı", Detail = $"{away.Team} {away.PerformanceScore}/100" },
            new() { Label = "Gol eğilimi", Detail = goalTrend },
            new() { Label = "Maç temposu", Detail = tempo },
            new() { Label = "Momentum",    Detail = mom },
        };
    }
}
