using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Outlook;

/// <summary>Hero Outlook + AI Summary üretir (Görev #016). LLM anlatısı varsa Hero'da kullanılır.</summary>
public static class OutlookSummaryGenerator
{
    /// <summary>
    /// Denge/tempo HESAPLANAMADIYSA (yeterli maç yok) üstünlük ya da tempo cümlesi KURULMAZ.
    /// Eskiden null yerine 0/boş değerler geliyor ve "dar bir X üstünlüğü; temkinli bir maç"
    /// diye ölçülmemiş bir görünüm yazılıyordu (ölçüldü: Malaga, 0 maç).
    /// </summary>
    public static string Hero(int? balance, string tempo, string homeName, string awayName, string? narrative)
    {
        if (!string.IsNullOrWhiteSpace(narrative)) return narrative;
        if (balance == null && string.IsNullOrWhiteSpace(tempo)) return "";

        var lean = balance == null ? null
                 : balance < 42 ? $"Dar bir {homeName} üstünlüğü"
                 : balance > 58 ? $"Dar bir {awayName} üstünlüğü"
                 : "Dengeli bir görünüm";

        var tempoWord = tempo == "Yüksek" ? "yüksek tempolu ve karşılıklı gollü"
                      : tempo == "Orta" ? "dengeli tempolu"
                      : tempo == "Düşük" ? "temkinli"
                      : null;

        if (lean == null) return $"{char.ToUpperInvariant(tempoWord![0])}{tempoWord[1..]} bir maç bekleniyor.";
        if (tempoWord == null) return $"{lean} öne çıkıyor.";
        return $"{lean}; {tempoWord} bir maç bekleniyor.";
    }

    /// <summary>Yalnız ÖLÇÜLEBİLMİŞ kalemler cümleye girer; ölçülemeyen kalem hiç anılmaz.</summary>
    public static string Summary(
        int? balance, string tempo, string momentum, string goalTrend,
        OutlookTeamDto home, OutlookTeamDto away)
    {
        var leanTeam = balance == null ? null
                     : balance < 42 ? home.Team
                     : balance > 58 ? away.Team
                     : null;
        var momTeam = momentum == "home" ? home.Team : momentum == "away" ? away.Team : null;

        var s = balance == null
            ? ""
            : leanTeam != null
                ? $"Genel görünüm dar bir {leanTeam} üstünlüğüne işaret ediyor"
                : "Genel görünüm dengeli";

        if (momTeam != null)
            s += s.Length == 0 ? $"Momentum {momTeam} tarafında" : $"; momentum {momTeam} tarafında";

        var tail = new List<string>();
        if (!string.IsNullOrWhiteSpace(tempo)) tail.Add($"Tempo {tempo.ToLowerInvariant()}");
        if (!string.IsNullOrWhiteSpace(goalTrend)) tail.Add($"{goalTrend.ToLowerInvariant()} eğilimi öne çıkıyor");

        if (s.Length > 0) s += ".";
        if (tail.Count > 0) s += (s.Length > 0 ? " " : "") + string.Join(", ", tail) + ".";
        return s.Trim();
    }
}

/// <summary>Evidence: Form, Performans, Gol eğilimi, Tempo, Momentum (Görev #016).</summary>
public static class OutlookEvidenceBuilder
{
    /// <summary>
    /// ÖLÇÜLEMEYEN KALEM LİSTEYE GİRMEZ. "Malaga 0/100" gibi bir satır kullanıcıya
    /// ölçülmüş bir performans gibi görünüyordu; oysa o takımın hiç maçı yoktu.
    /// Momentum çözülemediğinde "Dengeli" DEMEK de yanlıştır — denge bir ölçümdür.
    /// </summary>
    public static List<OutlookEvidenceItemDto> Build(
        OutlookTeamDto home, OutlookTeamDto away, string goalTrend, string tempo, string momentum)
    {
        var items = new List<OutlookEvidenceItemDto>();

        var homeForm = string.Join("", home.Form);
        var awayForm = string.Join("", away.Form);
        if (homeForm.Length > 0 || awayForm.Length > 0)
        {
            var parts = new List<string>();
            if (homeForm.Length > 0) parts.Add($"{home.Team} {homeForm}");
            if (awayForm.Length > 0) parts.Add($"{away.Team} {awayForm}");
            items.Add(new() { Label = "Son form", Detail = string.Join(" · ", parts) });
        }

        if (home.PerformanceScore.HasValue)
            items.Add(new() { Label = "İç saha performansı", Detail = $"{home.Team} {home.PerformanceScore}/100" });
        if (away.PerformanceScore.HasValue)
            items.Add(new() { Label = "Deplasman performansı", Detail = $"{away.Team} {away.PerformanceScore}/100" });

        if (!string.IsNullOrWhiteSpace(goalTrend))
            items.Add(new() { Label = "Gol eğilimi", Detail = goalTrend });
        if (!string.IsNullOrWhiteSpace(tempo))
            items.Add(new() { Label = "Maç temposu", Detail = tempo });

        if (momentum is "home" or "away" or "balanced")
            items.Add(new()
            {
                Label = "Momentum",
                Detail = momentum == "home" ? home.Team : momentum == "away" ? away.Team : "Dengeli"
            });

        return items;
    }
}
