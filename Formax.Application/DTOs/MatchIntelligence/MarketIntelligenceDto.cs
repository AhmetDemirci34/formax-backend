namespace Formax.Application.DTOs.MatchIntelligence;

// ─────────────────────────────────────────────────────────────────────────────
// Market Intelligence Engine çıktısı (Görev #017).
//
// FORMAX BAHİS UYGULAMASI DEĞİLDİR: oran/bahis tipi/kupon YOK. Yalnız "oynanma yoğunluğu"
// (OynanmaSkoru 0..100, 50=denge) zaman serisi + Sapma (ölçüm farkı) yorumlanır.
// Üretilen: eğilim, açılış→güncel, volatilite, kararlılık, sapma, AI güveni, AI özeti, evidence.
//
// NOT: Bu tür, mevcut DTOs.Matches.MarketIntelligenceDto (Headline/Detail/Tone) ile AYNI ADLI ama
// FARKLI namespace'tedir (Experience bazlı zengin çıktı).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class MarketIntelligenceDto
{
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";

    public string MarketTrend { get; init; } = "";
    public string OpeningState { get; init; } = "";
    public string CurrentState { get; init; } = "";
    /// <summary>home | away | balanced.</summary>
    public string Direction { get; init; } = "balanced";
    /// <summary>0 = ev sahibi yönünde, 50 = dengeli, 100 = deplasman yönünde.</summary>
    public int Lean { get; init; }

    public int Stability { get; init; }
    public int Volatility { get; init; }

    public MarketDeviationDto Deviation { get; init; } = new();
    public MarketChangeDto SignificantChange { get; init; } = new();

    public MarketConfidenceDto ConfidenceSummary { get; init; } = new();
    public int Confidence { get; init; }

    public string HeroSummary { get; init; } = "";
    public string AiSummary { get; init; } = "";
    public List<MarketEvidenceItemDto> Evidence { get; init; } = new();
}

public sealed class MarketDeviationDto
{
    /// <summary>Sapma büyüklüğü (ölçüm farkı).</summary>
    public int Score { get; init; }
    public string Region { get; init; } = "";
    public string Note { get; init; } = "";
}

public sealed class MarketChangeDto
{
    public string Label { get; init; } = "";
    public string Note { get; init; } = "";
    /// <summary>home | away | balanced.</summary>
    public string Direction { get; init; } = "balanced";
}

public sealed class MarketConfidenceDto
{
    public string Label { get; init; } = "Güven";
    public int Value { get; init; }
}

public sealed class MarketEvidenceItemDto
{
    public string Label { get; init; } = "";
    public string Detail { get; init; } = "";
}
