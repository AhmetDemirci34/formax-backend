namespace Formax.Application.DTOs.MatchIntelligence;

// ─────────────────────────────────────────────────────────────────────────────
// Watch Live Engine çıktısı (Görev #018).
//
// Yalnız RESMİ/LİSANSLI yayın; yasa dışı kaynak YOK. Lig → resmi yayıncı curated eşlemesi
// (gerçek lisans bilgisi) + maç durumundan availability. Bilinen lig için spesifik yayıncı,
// bilinmeyen lig için uydurma YAPILMADAN generic "resmi yayıncı" (dürüst).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class WatchLiveDto
{
    public string Broadcaster { get; init; } = "";
    public string Platform { get; init; } = "";
    /// <summary>live | scheduled | available | unavailable.</summary>
    public string Availability { get; init; } = "scheduled";
    public string AvailabilityLabel { get; init; } = "";
    public string Region { get; init; } = "";
    public string StreamQuality { get; init; } = "";
    public string MatchCoverage { get; init; } = "";

    public string AiRecommendation { get; init; } = "";
    public string HeroSummary { get; init; } = "";
    public string AiSummary { get; init; } = "";

    /// <summary>Lig curated tabloda mı eşlendi (spesifik yayıncı) yoksa generic mi.</summary>
    public bool IsSpecificBroadcaster { get; init; }
    public int Confidence { get; init; }

    public List<WatchLiveEvidenceItemDto> Evidence { get; init; } = new();
}

public sealed class WatchLiveEvidenceItemDto
{
    public string Label { get; init; } = "";
    public string Detail { get; init; } = "";
}
