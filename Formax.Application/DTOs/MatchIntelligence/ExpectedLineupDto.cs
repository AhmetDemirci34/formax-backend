namespace Formax.Application.DTOs.MatchIntelligence;

// ─────────────────────────────────────────────────────────────────────────────
// Expected Lineup Engine çıktısı (Görev #013).
//
// Maç başlamadan önce bir takımın MUHTEMEL ilk 11'i AI tahminiyle üretilir:
// diziliş + oyuncular + oyuncu güveni + takım güveni + kadro dışı + gerekçeler.
// Statik/uydurma yok — resmi kadro varsa o, yoksa son maç kadrolarından türetilir.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ExpectedLineupDto
{
    public string TeamName { get; init; } = "";
    /// <summary>4-3-3 | 4-2-3-1 | 3-5-2 | 4-4-2 (frontend'in desteklediği set).</summary>
    public string Formation { get; init; } = "";
    public List<ExpectedPlayerDto> Players { get; init; } = new();
    /// <summary>Takım güveni (0–100).</summary>
    public int Confidence { get; init; }
    public List<ExpectedUnavailableDto> UnavailablePlayers { get; init; } = new();
    public List<string> Reasons { get; init; } = new();
    /// <summary>Kaynak: "Official" (açıklandı) | "Predicted" (geçmişten tahmin) | "Insufficient".</summary>
    public string Source { get; init; } = "Predicted";
}

public sealed class ExpectedPlayerDto
{
    public int Number { get; init; }
    public string Name { get; init; } = "";
    /// <summary>G | D | M | F (domain pozisyon kovaları).</summary>
    public string Position { get; init; } = "";
    /// <summary>Rol etiketi (Kaleci / Defans / Orta Saha / Forvet).</summary>
    public string Role { get; init; } = "";
    /// <summary>Oyuncu güveni (0–100) — son maçlarda başlama sıklığından.</summary>
    public int Confidence { get; init; }
}

public sealed class ExpectedUnavailableDto
{
    public string Name { get; init; } = "";
    /// <summary>Injured | Suspended | Doubtful.</summary>
    public string Status { get; init; } = "";
    public string Reason { get; init; } = "";
}
