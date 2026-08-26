namespace Formax.Application.DTOs.MatchIntelligence;

// ─────────────────────────────────────────────────────────────────────────────
// Match Outlook Engine çıktısı (Görev #016).
//
// Maçın genel görünümü backend'de üretilir: form, performans, gol eğilimi, tempo, denge,
// momentum, AI güveni, AI değerlendirmesi ve evidence. Frontend yalnız render eder.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class MatchOutlookDto
{
    public OutlookTeamDto Home { get; init; } = new();
    public OutlookTeamDto Away { get; init; } = new();

    public string GoalTrend { get; init; } = "";
    public string Tempo { get; init; } = "";
    /// <summary>
    /// 0 = ev sahibi baskın, 50 = dengeli, 100 = deplasman baskın. İki tarafta da yeterli
    /// maç yoksa null — hesaplanmamış dengeyi 50 ya da 0 diye sunmak ölçüm taklididir.
    /// </summary>
    public int? Balance { get; init; }
    /// <summary>home | away | balanced | unknown (kanıt yetersiz).</summary>
    public string Momentum { get; init; } = "unknown";

    public OutlookConfidenceDto ConfidenceSummary { get; init; } = new();
    /// <summary>Hero Outlook — AI'ın tek cümlelik genel görünümü.</summary>
    public string HeroOutlook { get; init; } = "";

    public string AiSummary { get; init; } = "";
    public List<OutlookEvidenceItemDto> Evidence { get; init; } = new();
    public int Confidence { get; init; }
}

public sealed class OutlookTeamDto
{
    public string Team { get; init; } = "";
    /// <summary>Son form (W/D/L, en yeni önce).</summary>
    public List<string> Form { get; init; } = new();
    /// <summary>Performans etiketi (ör. "İç sahada güçlü").</summary>
    public string Performance { get; init; } = "";
    /// <summary>
    /// Performans göstergesi (0–100). YETERLİ MAÇ YOKSA null — "0/100" bir performans
    /// değil, veri yokluğudur (bkz. FormEvidencePolicy).
    /// </summary>
    public int? PerformanceScore { get; init; }
    public double GoalsFor { get; init; }
    public double GoalsAgainst { get; init; }
}

public sealed class OutlookConfidenceDto
{
    public string Label { get; init; } = "Güven";
    public int Value { get; init; }
}

public sealed class OutlookEvidenceItemDto
{
    public string Label { get; init; } = "";
    public string Detail { get; init; } = "";
}
