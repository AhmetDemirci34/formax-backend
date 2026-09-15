using System.Text.Json.Serialization;

namespace Formax.Application.DTOs.Recommendations;

/// <summary>
/// Oran hareket yönü. Backend AI Engine hesaplar; frontend yalnız çizer.
/// String olarak serialize edilir (frontend "Up" | "Down" | "None" bekler).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OddsMovement
{
    None = 0,
    Up = 1,
    Down = 2
}

/// <summary>
/// Maç başına tek en güçlü AI sonucu (Discovery Engine hesaplar).
/// Hero / Günün AI Kombini / Sana Özel kartlarında kullanılır.
/// </summary>
public sealed class TopPredictionDto
{
    public string Market { get; init; } = "";
    public int Probability { get; init; }

    /// <summary>Bu marketin GERÇEK oranı; sağlayıcıda karşılığı yoksa null.</summary>
    public decimal? Odd { get; init; }
}

/// <summary>
/// Maç başına ilk 3 AI tahmini (Discovery Engine hesaplar). Oran/hareket
/// OddsSnapshot + OddsMovementSnapshot entity'lerinden gelir. Frontend HESAP YAPMAZ.
/// </summary>
public sealed class AiPredictionDto
{
    public string Market { get; init; } = "";
    public int Probability { get; init; }
    public string Confidence { get; init; } = "";
    /// <summary>Olasılık ailesi (MatchResult / TotalGoals / BothTeamsScore) ve başlığı — snapshot'tan.</summary>
    public string? Family { get; init; }
    public string? FamilyTitle { get; init; }

    /// <summary>
    /// GERÇEK market oranı (MatchMarketOdds). Sağlayıcıda karşılığı olmayan markette null —
    /// UI oran göstermez. Nullable ZORUNLU: 0m "oran yok"u değil "oran sıfır"ı ifade eder.
    /// </summary>
    public decimal? CurrentOdd { get; init; }
    public decimal? PreviousOdd { get; init; }
    public OddsMovement Movement { get; init; } = OddsMovement.None;
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Stadyum bilgisi (opsiyonel; backend doldurdukça dolar).</summary>
public sealed class StadiumDto
{
    public string? Name { get; init; }
    public string? City { get; init; }
    public string? ImageUrl { get; init; }
}
