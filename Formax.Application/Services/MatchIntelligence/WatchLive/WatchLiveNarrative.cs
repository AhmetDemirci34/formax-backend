using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.WatchLive;

/// <summary>Hero yayın özeti + AI özeti (Görev #018). Yalnız resmi yayın; oran/yasa dışı içermez.</summary>
public static class WatchLiveSummaryGenerator
{
    public static string Hero(BroadcasterAnalyzer.BroadcasterInfo b, string availLabel)
        => b.IsSpecific
            ? $"Maçı {b.Region}'da resmi yayıncıdan {b.Quality} kalitede izleyebilirsin; {availLabel.ToLowerInvariant()}."
            : $"Maçı resmi yayıncı üzerinden izleyebilirsin; {availLabel.ToLowerInvariant()}.";

    public static string Summary(BroadcasterAnalyzer.BroadcasterInfo b, string availability, string availLabel)
    {
        if (availability == "unavailable")
            return "Bu maçın canlı yayını şu an erişilebilir değil.";

        var who = b.IsSpecific ? b.Broadcaster : "resmi yayıncı";
        return $"Önerilen tek resmi seçenek {who}. {b.Coverage}, {availLabel.ToLowerInvariant()}. Ek platforma gerek yok.";
    }
}

/// <summary>Evidence: Yayıncı, Platform, Bölge, Kalite, Durum (Görev #018).</summary>
public static class WatchLiveEvidenceBuilder
{
    public static List<WatchLiveEvidenceItemDto> Build(BroadcasterAnalyzer.BroadcasterInfo b, string availLabel)
        => new()
        {
            new() { Label = "Yayıncı", Detail = b.Broadcaster },
            new() { Label = "Platform", Detail = b.Platform },
            new() { Label = "Bölge", Detail = b.Region },
            new() { Label = "Kalite", Detail = b.Quality },
            new() { Label = "Durum", Detail = availLabel },
        };
}
