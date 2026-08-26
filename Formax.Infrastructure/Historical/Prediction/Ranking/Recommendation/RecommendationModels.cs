using System.Collections.Generic;
using Formax.Infrastructure.Historical.Prediction.Confidence;
using Formax.Infrastructure.Historical.Prediction.Ranking.Discovery;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Recommendation;

/// <summary>
/// Recommendation Engine'in KAYNAK-AGNOSTİK girdisi. MEVCUT katmanların çıktılarını taşır (hiçbirini değiştirmez):
/// Discovery Feed item (sıralama korunur), Base Radar Score breakdown (sinyaller), olasılık, confidence.
/// Recommendation yalnız AÇIKLAMA üretir — sıralamaya dokunmaz.
/// </summary>
public sealed record RecommendationInput
{
    public required int MatchId { get; init; }
    /// <summary>Discovery Feed item (rank/score/hidden gem/personal/base) — DEĞİŞTİRİLMEZ.</summary>
    public required DiscoveryFeedItem Discovery { get; init; }
    /// <summary>Base Radar Score (Competitiveness/GoalPotential/Form/Stakes/... breakdown kaynağı) — DEĞİŞTİRİLMEZ.</summary>
    public required RadarScore Radar { get; init; }
    public required double[] Probability { get; init; }
    public required ConfidenceAssessment Confidence { get; init; }
}

/// <summary>Öneri gücü seviyesi.</summary>
public enum RecommendationLevel
{
    Low,
    Optional,
    Recommended,
    StronglyRecommended
}

/// <summary>
/// Recommendation Engine config'i — TAMAMEN config-driven. Skor karışım ağırlıkları + tag eşikleri + seviye
/// bantları. Deterministik.
/// </summary>
public sealed class RecommendationWeights
{
    // ── Recommendation Score karışımı (Discovery Score'u DEĞİŞTİRMEZ; ayrı açıklama skoru) ──
    public double RelevanceWeight { get; set; } = 1.0;       // Personal Radar Score
    public double ConfidenceWeight { get; set; } = 0.5;
    public double DistinctivenessWeight { get; set; } = 0.8; // en güçlü sinyal
    public double HiddenGemBonus { get; set; } = 6.0;

    // ── Tag eşikleri (normalize sinyal değeri) — veri yoksa tag üretilmez ──
    public double TightMatchThreshold { get; set; } = 0.70;   // Competitiveness
    public double GoalFestThreshold { get; set; } = 0.65;     // GoalPotential
    public double InFormThreshold { get; set; } = 0.65;       // Form
    public double UpsetThreshold { get; set; } = 0.55;        // Upset
    public double EliteStrengthThreshold { get; set; } = 0.70;// TeamStrength
    public double EliteLeagueThreshold { get; set; } = 0.60;  // LeagueQuality
    public double HighConfidenceThreshold { get; set; } = 0.55;// Confidence
    public double TitleRaceThreshold { get; set; } = 0.65;    // Stakes
    public double H2HThreshold { get; set; } = 0.60;          // H2HIntensity
    public double DerbyThreshold { get; set; } = 0.50;        // Derby (veri gelince)
    public double InterestPointsThreshold { get; set; } = 0.5;// Personal−Base farkı (puan)

    // ── Seviye bantları ──
    public double StronglyAt { get; set; } = 75;
    public double RecommendedAt { get; set; } = 55;
    public double OptionalAt { get; set; } = 35;

    public static RecommendationWeights Default => new();
}

/// <summary>
/// Bir maç için öneri: skor + seviye + tag'ler + gerekçeler + breakdown. Discovery sıralamasını DEĞİŞTİRMEZ
/// (ayrı açıklama katmanı). Tag/gerekçe yalnız GERÇEK sinyallerden üretilir (veri yoksa üretilmez).
/// </summary>
public sealed record MatchRecommendation
{
    public required int MatchId { get; init; }
    public double RecommendationScore { get; init; }
    public RecommendationLevel Level { get; init; }
    public bool HiddenGem { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
    public required IReadOnlyList<SignalContribution> Breakdown { get; init; }

    // ── Korunan Discovery/Radar değerleri (referans; değiştirilmez) ──
    public int DiscoveryRank { get; init; }
    public double DiscoveryScore { get; init; }
    public double PersonalScore { get; init; }
    public double BaseScore { get; init; }
}
