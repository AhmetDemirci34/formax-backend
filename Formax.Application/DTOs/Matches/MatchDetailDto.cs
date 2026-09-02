using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Formax.Application.DTOs.Live;
using Formax.Application.DTOs.Lineup;
using Formax.Application.DTOs.Standings;
using Formax.Application.DTOs.Nabiz;

namespace Formax.Application.DTOs.Matches;

public class MatchDetailDto
{
    public int MatchId { get; set; }

    public TeamSummaryDto HomeTeam { get; set; } = new();
    public TeamSummaryDto AwayTeam { get; set; } = new();

    public DateTime MatchDate { get; set; }
    public string Status { get; set; } = string.Empty;

    // ── MAÇ SONRASI ÖZET (02.09.2026) — additive ────────────────────────────────
    // Maç bittiğinde aynı rota (/match/{id}) maç öncesi detay yerine ÖZET gösterir.
    // Bu alanlar yalnız Finished maçta dolar; başlamamış maçta null/boş kalır ve
    // mevcut maç öncesi sözleşme AYNEN korunur.

    /// <summary>İY / 2Y / MS kırılımı. Maç bitmediyse null.</summary>
    public MatchScoreBreakdownDto? ScoreBreakdown { get; set; }

    /// <summary>
    /// Önemli anlar — kronolojik, tekilleştirilmiş. Depoda olay yoksa BOŞ liste
    /// (uydurma olay üretilmez; ekran dürüst boş durum gösterir).
    /// </summary>
    public List<MatchEventDto> Events { get; set; } = new();

    /// <summary>
    /// MAÇ VİDEOLARI — özet, gol ve önemli an. Yalnız DOĞRULANMIŞ resmî kayıtlar.
    ///
    /// MAÇ SONRASI HABER YOKTUR (02.09.2026 ürün kararı): bitmiş maç ekranı haber,
    /// teknik direktör/oyuncu açıklaması ve basın/sosyal yorum GÖSTERMEZ. Bu alanın
    /// bir haber karşılığı bilerek bırakılmamıştır — boş bir başlık bile gösterilmez.
    /// </summary>
    public List<MatchVideoDto> Videos { get; set; } = new();

    /// <summary>
    /// MAÇ İSTATİSTİKLERİ — yalnız GERÇEK veri varsa dolu, aksi hâlde null.
    ///
    /// Depoda satır olması veri olduğu anlamına gelmez: bütün alanları sıfır olan bir
    /// canlı-istatistik satırı "0 şut, %0 topa sahip olma" değil, VERİ YOK demektir.
    /// Karar burada bir kez verilir; ekran bölümü null ise hiç render etmez.
    /// </summary>
    public MatchStatisticsDto? Statistics { get; set; }

    // ── Match meta ──────────────────────────────────────────────────────────────
    public string League { get; set; } = string.Empty;
    /// <summary>Sağlayıcının HAM tur adı ("3rd Qualifying Round", "Regular Season - 1").</summary>
    public string? Round { get; set; }

    /// <summary>
    /// MAÇ TÜRÜ — ham tur adından türeyen Türkçe etiket ("Eleme Turu", "Son 16 Turu",
    /// "Lig Maçı · 1. Hafta", "Final"). Sağlayıcı tur vermediyse veya tur adı tanınmıyorsa
    /// null; UI o zaman tür satırını GÖSTERMEZ (tahmin edilmez).
    /// </summary>
    public string? MatchTypeLabel { get; set; }
    public string? Referee { get; set; }
    public string? Venue { get; set; }
    public string? Weather { get; set; }
    public int WatchersCount { get; set; }

    // ── Form history (takımın KENDİ ulusal ligindeki son oynanmış maçlar) ───────
    public List<LastMatchDto> HomeTeamLastMatches { get; set; } = new();
    public List<LastMatchDto> AwayTeamLastMatches { get; set; } = new();

    /// <summary>
    /// Yukarıdaki form listesinin süzüldüğü ligin GERÇEK adı (ör. "Süper Lig").
    /// null = takımın ligi çözülemedi → liste süzülmedi ve "ligde" denemez.
    /// </summary>
    public string? HomeTeamFormLeague { get; set; }
    public string? AwayTeamFormLeague { get; set; }

    /// <summary>
    /// MEVCUT SEZON LİG FORMU — yukarıdaki listenin ÖZETİ ve anlatının dayanağı.
    /// Kapsam: aynı lig + bu sezon + maç saatinden önce + tamamlanmış maçlar.
    /// Önceki sezon, hazırlık, kupa ve Avrupa maçları BU ÖZETE GİRMEZ; eksik maç
    /// başka kaynaktan tamamlanmaz. Sezon çözülemezse Played=0 ve HasNoData=true.
    /// </summary>
    public TeamSeasonFormDto? HomeSeasonForm { get; set; }
    public TeamSeasonFormDto? AwaySeasonForm { get; set; }

    // ── Analysis ────────────────────────────────────────────────────────────────
    public ComparisonDto Comparison { get; set; } = new();
    [JsonPropertyName("h2h")]
    public H2HDto H2H { get; set; } = new();
    public InsightDto Insight { get; set; } = new();

    public SapmaDto Sapma { get; set; } = new();
    public AiDto Ai { get; set; } = new();
    public UserProtectionDto UserProtection { get; set; } = new();

    // ── AI intelligence sections ─────────────────────────────────────────────────
    public List<ProbabilityItemDto> Probabilities { get; set; } = new();
    public List<KeyMatchupDto> KeyMatchups { get; set; } = new();
    public MarketIntelligenceDto MarketIntelligence { get; set; } = new();
    public RiskIntelligenceDto RiskIntelligence { get; set; } = new();
    public TacticalMatchupDto TacticalMatchup { get; set; } = new();

    // ── Sprint 1: Lineup & player status ────────────────────────────────────────
    public LineupSectionDto Lineup { get; set; } = new();
    public PlayerStatusSectionDto PlayerStatus { get; set; } = new();

    // ── Sprint 2: Standings & competition context ────────────────────────────────
    public StandingSectionDto? Standing { get; set; }
    public CompetitionContextSectionDto? CompetitionContext { get; set; }

    // ── Sprint 3: Live match intelligence ────────────────────────────────────────
    public LiveSectionDto Live { get; set; } = new();

    // ── Sprint 4: NABIZ feed intelligence ────────────────────────────────────────
    public NabizSectionDto NabizFeed { get; set; } = new();

    // ── Radar v2: LLM Match Intelligence narrative (opsiyonel; gelmezse UI eski davranış)
    public RadarNarrativeDto? AiNarrative { get; set; }
}

// TeamSummaryDto and LastMatchDto are defined in their own files
// (TeamSummaryDto.cs, LastMatchDto.cs) in this namespace.

// ────────────────────────────────────────────────────────────────────────────────
// Comparison
// ────────────────────────────────────────────────────────────────────────────────

public class ComparisonDto
{
    public TeamComparisonDto Home { get; set; } = new();
    public TeamComparisonDto Away { get; set; } = new();
}

public class TeamComparisonDto
{
    public double AvgGoalsFor { get; set; }
    public double AvgGoalsAgainst { get; set; }
    public int GoalScoringRate { get; set; }
    public int CleanSheetRate { get; set; }
    public double HomeAwayAvgGoals { get; set; }
    public int FormScore { get; set; }
    public int LeagueRank { get; set; }

    /// <summary>
    /// Bu metriklerin hesaplandığı GERÇEK maç sayısı (0–10).
    ///
    /// Neden gerekli: veri yokluğu ile "gerçekten sıfır" aynı şey değildir. Kayıt yoksa tüm
    /// oranlar 0 dönüyordu ve karşılaştırma bunu gerçek bir fark sanıyordu (ölçüldü:
    /// Deportivo 0 maç → "Elche %90 ile üstün" gibi yanıltıcı cümle). Tüketiciler yeterli
    /// örnek olup olmadığını bu alandan anlar.
    /// </summary>
    public int SampleCount { get; set; }

    /// <summary>
    /// Örneklemdeki EN YENİ maçın tarihi (UTC). Kaç maç olduğu kadar NE ZAMAN oynandığı da
    /// gerekir: beş maç 2024'ten geliyorsa bu "güncel form" değildir ve "galibiyet hasreti"
    /// gibi zamansal kesinlik içeren cümleler kurulamaz (ölçüldü: Lask Linz, 607 gün).
    /// Kayıt yoksa null.
    /// </summary>
    public DateTime? NewestMatchUtc { get; set; }

    /// <summary>
    /// Örneklemin geldiği turnuvalar. Form yalnız kapsam içi liglerden hesaplanır; bir takımın
    /// ulusal ligi kapsam dışıysa örneklem tümüyle Avrupa kupalarından oluşur ve "son beş maçı"
    /// ifadesi aslında "kapsamımdaki son beş maç" demektir. Tüketici bunu bilmelidir.
    /// </summary>
    public List<string> Competitions { get; set; } = new();
}

// H2HDto ve H2HMatchDto → Formax.Application/DTOs/Matches/H2HDto.cs (Sprint 20B)

// ────────────────────────────────────────────────────────────────────────────────
// Insight / AI
// ────────────────────────────────────────────────────────────────────────────────

public class InsightDto
{
    public string Headline { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public class SapmaDto
{
    public int OynanmaSkoru { get; set; }
    public int GucSkoru { get; set; }
    public int Sapma { get; set; }
    public string OynanmaYonu { get; set; } = string.Empty;
    public string GercekGucYonu { get; set; } = string.Empty;
    public string SapmaBolgesi { get; set; } = string.Empty;
    public bool SessizMi { get; set; }
    public string SapmaMetni { get; set; } = string.Empty;
}

public class AiDto
{
    public string State { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public class UserProtectionDto
{
    public string ResponsibilityNote { get; set; } = string.Empty;
    public bool DecisionIsYours { get; set; }
}

// ────────────────────────────────────────────────────────────────────────────────
// AI intelligence — probability cards
// ────────────────────────────────────────────────────────────────────────────────

public class ProbabilityItemDto
{
    /// <summary>Market label: "KG VAR", "2.5 ÜST", "GS KAYBETMEZ", etc.</summary>
    public string Market { get; set; } = string.Empty;

    /// <summary>Derived probability 0–100.</summary>
    public int Probability { get; set; }

    /// <summary>Confidence tier: "YÜKSEK" | "ORTA" | "DÜŞÜK"</summary>
    public string Confidence { get; set; } = string.Empty;
}

// ────────────────────────────────────────────────────────────────────────────────
// AI intelligence — key matchups
// ────────────────────────────────────────────────────────────────────────────────

public class KeyMatchupDto
{
    public string HomePlayer { get; set; } = string.Empty;
    public string AwayPlayer { get; set; } = string.Empty;
    public string HomePosition { get; set; } = string.Empty;
    public string AwayPosition { get; set; } = string.Empty;
    public string MatchupContext { get; set; } = string.Empty;
}

// ────────────────────────────────────────────────────────────────────────────────
// AI intelligence — market intelligence
// ────────────────────────────────────────────────────────────────────────────────

public class MarketIntelligenceDto
{
    public string Headline { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;

    /// <summary>"positive" | "negative" | "neutral"</summary>
    public string Tone { get; set; } = "neutral";
}

// ────────────────────────────────────────────────────────────────────────────────
// AI intelligence — risk intelligence
// ────────────────────────────────────────────────────────────────────────────────

public class RiskIntelligenceDto
{
    public string HomeRiskLabel { get; set; } = string.Empty;
    public string HomeRiskDetail { get; set; } = string.Empty;
    public string AwayRiskLabel { get; set; } = string.Empty;
    public string AwayRiskDetail { get; set; } = string.Empty;
}

// ────────────────────────────────────────────────────────────────────────────────
// AI intelligence — tactical matchup (dual bars)
// ────────────────────────────────────────────────────────────────────────────────

public class TacticalMatchupDto
{
    public TacticalDimension Attack { get; set; } = new();
    public TacticalDimension Defense { get; set; } = new();
    public TacticalDimension Transition { get; set; } = new();
    public TacticalDimension SetPiece { get; set; } = new();
    public TacticalDimension Form { get; set; } = new();
    public TacticalDimension Discipline { get; set; } = new();
}

public class TacticalDimension
{
    public string Label { get; set; } = string.Empty;

    /// <summary>Home score 0–10.</summary>
    public double HomeScore { get; set; }

    /// <summary>Away score 0–10.</summary>
    public double AwayScore { get; set; }
}
