namespace Formax.Application.DTOs.MatchIntelligence;

// ─────────────────────────────────────────────────────────────────────────────
// Live Intelligence Engine çıktısı (Görev #019).
//
// Maç oynanırken canlı verileri (skor, dakika, shots/dangerousAttacks, momentum, olaylar)
// analiz eder: maç ritmi, momentum, en önemli olay, AI canlı özeti, evidence.
// Canlı veri yoksa dürüst fallback (IsLive=false). Uydurma YOK.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class LiveIntelligenceDto
{
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";

    /// <summary>Canlı veri var mı? false ise maç öncesi/sonrası (fallback).</summary>
    public bool IsLive { get; init; }

    public string MatchMinute { get; init; } = "";
    public LiveScoreDto Score { get; init; } = new();
    /// <summary>live | halftime | ended | pre.</summary>
    public string MatchStatus { get; init; } = "pre";
    public string StatusLabel { get; init; } = "";

    /// <summary>Düşük | Orta | Yüksek.</summary>
    public string MatchRhythm { get; init; } = "";
    /// <summary>home | away | balanced.</summary>
    public string Momentum { get; init; } = "balanced";
    /// <summary>0 = ev sahibi, 50 = dengeli, 100 = deplasman.</summary>
    public int MomentumValue { get; init; }

    public LiveKeyEventDto KeyEvent { get; init; } = new();

    public int Confidence { get; init; }
    public string HeroSummary { get; init; } = "";
    public string AiSummary { get; init; } = "";
    public List<LiveEvidenceItemDto> Evidence { get; init; } = new();
}

public sealed class LiveScoreDto
{
    public int Home { get; init; }
    public int Away { get; init; }
}

public sealed class LiveKeyEventDto
{
    public string Minute { get; init; } = "";
    public string Type { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>home | away.</summary>
    public string Team { get; init; } = "home";
    public bool HasEvent { get; init; }
}

public sealed class LiveEvidenceItemDto
{
    public string Label { get; init; } = "";
    public string Detail { get; init; } = "";
}
