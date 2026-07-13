using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Live;

/// <summary>Canlı Hero özeti + AI özeti (Görev #019).</summary>
public static class LiveSummaryGenerator
{
    public static string Hero(
        int homeScore, int awayScore, string minute, string momentum,
        string homeName, string awayName, LiveKeyEventDto keyEvent)
    {
        var momTeam = momentum == "home" ? homeName : momentum == "away" ? awayName : null;
        var s = $"{homeName} {homeScore}-{awayScore} {awayName}" + (string.IsNullOrEmpty(minute) ? "." : $" ({minute}).");
        if (keyEvent.HasEvent) s += $" Son önemli gelişme: {keyEvent.Minute} {keyEvent.Type.ToLowerInvariant()}.";
        if (momTeam != null) s += $" Momentum {momTeam} tarafında.";
        return s;
    }

    public static string Summary(
        int homeScore, int awayScore, string minute, string rhythm, string momentum,
        string homeName, string awayName, LiveKeyEventDto keyEvent)
    {
        var momWord = momentum == "home" ? $"{homeName} tarafında"
                    : momentum == "away" ? $"{awayName} tarafında"
                    : "dengede";
        var s = $"Skor {homeScore}-{awayScore}" + (string.IsNullOrEmpty(minute) ? "" : $" ({minute})") + ".";
        if (keyEvent.HasEvent) s += $" En önemli gelişme {keyEvent.Minute} {keyEvent.Type.ToLowerInvariant()};";
        s += $" ritim {rhythm.ToLowerInvariant()}, momentum {momWord}.";
        return s;
    }

    public static string Fallback(string statusLabel)
        => $"Maç şu an canlı değil ({statusLabel.ToLowerInvariant()}); başladığında en önemli gelişmeyi ve momentum değişimlerini canlı yorumlayacağım.";
}

/// <summary>Evidence: Skor, Dakika, Momentum, Son önemli olay, Maç ritmi (Görev #019).</summary>
public static class LiveEvidenceBuilder
{
    public static List<LiveEvidenceItemDto> Build(
        int homeScore, int awayScore, string minute, string momentum,
        LiveKeyEventDto keyEvent, string rhythm, string homeName, string awayName)
    {
        var momLabel = momentum == "home" ? homeName : momentum == "away" ? awayName : "Dengeli";
        return new()
        {
            new() { Label = "Skor", Detail = $"{homeScore} - {awayScore}" },
            new() { Label = "Dakika", Detail = string.IsNullOrEmpty(minute) ? "—" : minute },
            new() { Label = "Momentum", Detail = momLabel },
            new() { Label = "Son önemli olay", Detail = keyEvent.HasEvent ? $"{keyEvent.Minute} {keyEvent.Type}" : "yok" },
            new() { Label = "Maç ritmi", Detail = rhythm },
        };
    }

    public static List<LiveEvidenceItemDto> Fallback()
        => new() { new() { Label = "Durum", Detail = "Maç öncesi" } };
}
