using System.Text.Json.Serialization;

namespace Formax.Application.DTOs.MatchIntelligence;

// ─────────────────────────────────────────────────────────────────────────────
// Living Lineup Engine çıktısı (Görev #014).
//
// Resmi ilk 11 açıklandığında BEKLENEN (tarih tahmini) ile RESMİ (açıklanan) kadro
// backend'de karşılaştırılır. Frontend yalnız sonucu gösterir; karşılaştırma burada.
// Resmi kadro yoksa Announced=false ile graceful fallback.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class LivingLineupDto
{
    public string TeamName { get; init; } = "";

    /// <summary>Gösterilecek diziliş (resmi varsa resmi, yoksa beklenen).</summary>
    public string Formation { get; init; } = "";
    public string ExpectedFormation { get; init; } = "";
    public string OfficialFormation { get; init; } = "";

    /// <summary>Resmi kadro açıklandı mı? false ise karşılaştırma yapılmadı.</summary>
    public bool Announced { get; init; }

    /// <summary>Resmi 11 (durum etiketli): karşılaştırmanın sonucu.</summary>
    public List<LivingPlayerDto> Players { get; init; } = new();

    /// <summary>Beklenen 11 (baz çizgisi) — mapper lineupChange.expected için (wire'da tekrar edilmez).</summary>
    [JsonIgnore]
    public List<ExpectedPlayerDto> ExpectedPlayers { get; init; } = new();
    /// <summary>Resmi 11 (ham) — mapper lineupChange.official için (wire'da tekrar edilmez).</summary>
    [JsonIgnore]
    public List<ExpectedPlayerDto> OfficialPlayers { get; init; } = new();

    // Oyuncu bazlı fark
    public List<LivingPlayerDto> AddedPlayers { get; init; } = new();      // İlk 11'e girdi
    public List<LivingPlayerDto> RemovedPlayers { get; init; } = new();    // İlk 11'den çıktı
    public List<LivingPlayerDto> MovedPlayers { get; init; } = new();      // Pozisyon değişti
    public List<LivingPlayerDto> UnchangedPlayers { get; init; } = new();  // Aynı kaldı

    // Takım bazlı analiz
    public bool FormationChanged { get; init; }
    public int ChangeCount { get; init; }
    public List<string> AffectedLines { get; init; } = new(); // "Savunma"/"Orta Saha"/"Hücum"
    public int Confidence { get; init; }

    public string AiSummary { get; init; } = "";
    public List<LivingEvidenceDto> Evidence { get; init; } = new();
}

public sealed class LivingPlayerDto
{
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public string Position { get; init; } = "";        // G/D/M/F
    public string Role { get; init; } = "";
    /// <summary>same | in | moved | out.</summary>
    public string Status { get; init; } = "same";
    /// <summary>"moved" ise beklenen (önceki) pozisyon.</summary>
    public string? FromPosition { get; init; }
}

public sealed class LivingEvidenceDto
{
    public string Label { get; init; } = "";
    public string Detail { get; init; } = "";
}
