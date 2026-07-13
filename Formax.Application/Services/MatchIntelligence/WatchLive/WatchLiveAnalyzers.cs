namespace Formax.Application.Services.MatchIntelligence.WatchLive;

/// <summary>
/// Lig → RESMİ/LİSANSLI yayıncı eşlemesi (Görev #018). Yalnız güvenle bilinen gerçek lisans bilgisi;
/// bilinmeyen lig için uydurma YAPMAZ, generic "resmi yayıncı" döner. Yasa dışı kaynak yok.
/// </summary>
public static class BroadcasterAnalyzer
{
    public sealed record BroadcasterInfo(
        string Broadcaster, string Platform, string Region, string Quality, string Coverage, bool IsSpecific);

    private static readonly (string[] Keywords, BroadcasterInfo Info)[] Table =
    {
        (new[] { "süper lig", "super lig", "turkish super" },
            new BroadcasterInfo("beIN SPORTS", "beIN CONNECT · web, mobil, TV", "Türkiye", "HD 1080p", "Tam maç · canlı", true)),
        (new[] { "trendyol 1. lig", "1. lig", "türkiye kupası" },
            new BroadcasterInfo("beIN SPORTS", "beIN CONNECT", "Türkiye", "HD", "Tam maç · canlı", true)),
    };

    public static BroadcasterInfo Resolve(string? league)
    {
        var l = (league ?? "").ToLowerInvariant();
        foreach (var (kw, info) in Table)
            if (!string.IsNullOrWhiteSpace(l) && kw.Any(k => l.Contains(k)))
                return info;

        // Bilinmeyen/boş lig → uydurma yok; generic resmi seçenek.
        return new BroadcasterInfo("Resmi yayıncı", "Lisanslı platform", "Bölgeye göre değişir", "Resmi kalite", "Tam maç · canlı", false);
    }
}

/// <summary>Yayın durumu: maç Status + tarihine göre (Yayında / Yakında / Aktif / Kullanılamıyor).</summary>
public static class AvailabilityAnalyzer
{
    public static (string Availability, string Label) Analyze(string status, DateTime matchUtc, DateTime nowUtc)
    {
        var s = (status ?? "").ToLowerInvariant();
        if (s.Contains("live")) return ("live", "Yayında");
        if (s.Contains("finish") || s.Contains("ended") || s.Contains("ft")) return ("unavailable", "Yayın sona erdi");
        if (s.Contains("cancel")) return ("unavailable", "Maç iptal edildi");
        if (s.Contains("postpon")) return ("unavailable", "Maç ertelendi");

        var mins = (matchUtc - nowUtc).TotalMinutes;
        if (mins <= 30 && mins > -5) return ("available", "Yayına yakında açılıyor");
        return ("scheduled", "Maç saatinde aktif");
    }
}

/// <summary>En uygun resmi seçenek için AI önerisi (yasa dışı/oran içermez).</summary>
public static class RecommendationEngine
{
    public static string Recommend(BroadcasterAnalyzer.BroadcasterInfo b, string availability)
    {
        if (availability == "unavailable")
            return "Maçın canlı yayını şu an erişilebilir değil.";

        if (b.IsSpecific)
            return availability == "live"
                ? $"En uygun seçenek {b.Broadcaster}: {b.Region}'da {b.Quality} kalitede canlı izleyebilirsin."
                : $"En uygun seçenek {b.Broadcaster}: {b.Region}'da {b.Quality} kalitede, tam maç; yayın maç saatinde açılır.";

        return "Maçı resmi yayıncı üzerinden izleyebilirsin; bölgende lisanslı yayıncıyı kontrol et.";
    }
}
