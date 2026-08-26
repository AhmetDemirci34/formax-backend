using Formax.Application.DTOs.Teams;
using Formax.Engine.Core.ExternalTrends;

namespace Formax.Application.DTOs.Recommendations;

public class RecommendationCardDto
{
    public int MatchId { get; set; }

    /// <summary>Kickoff zamanı (Match.MatchDate). Yalnızca mevcut alan feed'e taşınır — yeni hesaplama yok.</summary>
    public DateTime? MatchDate { get; set; }

    public TeamDto HomeTeam { get; set; } = new();
    public TeamDto AwayTeam { get; set; } = new();

    public string TeamA { get; set; } = "";
    public string TeamB { get; set; } = "";

    public string LeagueName { get; set; } = "";

    /// <summary>League position (1-based). Null when unknown. Importance signal only.</summary>
    public int? HomeRank { get; set; }
    public int? AwayRank { get; set; }

    public double Score { get; set; }
    public double RecommendationScore { get; set; }

    public double ConfidenceScore { get; set; }
    public string ConfidenceLabel { get; set; } = "";

    public string CardType { get; set; } = "USER";
    public string PersonalReason { get; set; } = "";

    /// <summary>
    /// Deterministic explanation code for the dominant ranking factor.
    /// One of: FOLLOWED_TEAM | HIGH_INTEREST | TRENDING | MARKET_SIGNAL | GLOBAL_SIGNAL.
    /// Not AI-generated.
    /// </summary>
    public string RecommendationReason { get; set; } = "";

    public TrendDto Trend { get; set; } = new();
    public ExternalDto External { get; set; } = new();

    public ExternalTrendDto? ExternalTrend { get; set; }

    public string InsightLabel { get; set; } = "";
    public string InsightReason { get; set; } = "";
    public int Priority { get; set; }

    public double TrendWeight { get; set; }
    public double TrendImpact { get; set; }

    public double MarketTrendScore { get; set; } = 0;
    // UserTrendScore = DISPLAY davranışı (floor'lu, cold-start UI için). UI bunu gösterir.
    public double UserTrendScore { get; set; } = 0;
    // RawUserTrendScore = RANKING davranışı (floor'suz, gerçek UserTrendService çıktısı).
    // Home Feed sıralaması bunu okur; interest floor'la maskelenmeden yansır. Ayrı alan ZORUNLU:
    // UserTrendScore hem ranking hem UI tarafından okunuyor ve ihtiyaçları çelişiyor (raw vs floor'lu).
    public double RawUserTrendScore { get; set; } = 0;
    public double GlobalTrendScore { get; set; } = 0;

    // 🔥 NEW (Phase 7.5)
    public double ExternalMomentum { get; set; } = 0;

    public string Highlight { get; set; } = "";
    public string AiComment { get; set; } = "";
    public string AiSummary { get; set; } = "";

    // ── Keşfet anlatısı (Match Intelligence / Gemma) ─────────────────────────
    // Keşfet kartındaki "FORMAX AI Yorumu" bu iki alandan beslenir.
    //
    // NEDEN BURADA: Keşfet, teaser göstermek için maç detayını çağırıyordu ve o uç
    // TEK istekte ÜÇ yüzeyi birden ürettiği için kart görüntülemek 3 LLM çağrısına
    // mal oluyordu (gereksiz kota). Anlatı artık feed yanıtıyla taşınır.
    //
    // ÜRETİM TETİKLENMEZ: değerler yalnızca DAHA ÖNCE üretilmiş ve anlatı deposunda
    // duran snapshot'tan kopyalanır (bkz. IRadarNarrativeStore.TryGetLatestForMatch).
    // Snapshot yoksa alanlar BOŞ kalır ve kart AI yorumu bölümünü hiç göstermez —
    // uydurma metin üretilmez.

    /// <summary>Keşfet yüzeyinin kısa maç hikâyesi (RadarNarrative.RadarSummary).</summary>
    public string RadarSummary { get; set; } = "";

    /// <summary>Keşfet yüzeyinin öne çıkan satırları (RadarNarrative.Highlights).</summary>
    public List<string> RadarHighlights { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string StoryHeadline { get; set; } = "";
    public string StoryBody     { get; set; } = "";
    public double CrossUserScore { get; set; }
    public double MomentumScore { get; set; }
    public double SpikeScore { get; set; }
    public double DirectionScore { get; set; }

    // ── UI Surface Integration — already-computed Radar Intelligence, surfaced ──
    // Source: MatchIntelligenceSnapshot (ImportanceScore / SignalsJson) + Radar ranking.
    // No new engine/data; these only carry existing values the frontend already expects.
    /// <summary>Normalized 0-100 match importance (MatchIntelligenceSnapshot.ImportanceScore).</summary>
    public double MatchImportance { get; set; }

    /// <summary>0-100 Radar support score (IRadarRankingService.NormalizeRadarScore).</summary>
    public double RadarScore { get; set; }

    /// <summary>Top derived importance signals (MatchIntelligenceSnapshot.SignalsJson).</summary>
    public List<KeySignalDto> KeySignals { get; set; } = new();

    // ── Discovery Engine surface (tek DTO — Hero/Feed/Kombin/Sana Özel aynı DTO) ──
    // Backend Single Source Of Truth: frontend bu alanları YALNIZ render eder, HESAP YAPMAZ.

    /// <summary>Maç durumu (NotStarted | PreMatch | Live | Finished ...). Discovery filtresi için.</summary>
    public string Status { get; set; } = "";

    /// <summary>Maç canlı mı (Status == Live).</summary>
    public bool IsLive { get; set; }

    /// <summary>Canlı dakika (ör. 67). Yalnız canlı maçta; yoksa null.</summary>
    public int? LiveMinute { get; set; }

    /// <summary>Canlı skor — ev sahibi. Yalnız canlı maçta MatchLiveStats'tan taşınır; yoksa null.</summary>
    public int? HomeScore { get; set; }

    /// <summary>Canlı skor — deplasman. Yalnız canlı maçta MatchLiveStats'tan taşınır; yoksa null.</summary>
    public int? AwayScore { get; set; }

    /// <summary>0-100 AI Güven Skoru. Backend hesaplar (frontend confidenceScore×100 YAPMAZ).</summary>
    public int AiTrustScore { get; set; }

    /// <summary>Discovery Engine sıralama skoru (0-100). Hero/Feed sıralamasının tek kaynağı.</summary>
    public double DiscoveryScore { get; set; }

    /// <summary>Maçın tek en güçlü AI tahmini. Backend hesaplar; gelmezse null.</summary>
    public TopPredictionDto? TopPrediction { get; set; }

    /// <summary>Maçın ilk 3 AI tahmini (oran + hareket). Backend hesaplar; gelmezse boş.</summary>
    public List<AiPredictionDto> Predictions { get; set; } = new();

    /// <summary>Stadyum bilgisi (opsiyonel).</summary>
    public StadiumDto? Stadium { get; set; }
}

/// <summary>
/// Mirrors the frontend <c>KeySignal</c> contract (types/api.ts). Carrier only —
/// filled from existing MatchSignal data; no new signal is produced here.
/// </summary>
public class KeySignalDto
{
    public string? Icon { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Caption { get; set; }
    public string? Tone { get; set; }
}