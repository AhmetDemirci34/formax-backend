using System.Collections.Generic;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.HiddenGems;

/// <summary>
/// Hidden Gems config'i — TAMAMEN config-driven. Kalite sinyalleri + obscurity (radar-altılık) ağırlıkları +
/// eşikler. "Büyük takım hidden gem olamaz" hard cap ve "yalnız düşük skor gem yapmaz" için kalite tabanı buradan.
/// </summary>
public sealed class HiddenGemWeights
{
    // ── Kalite ağırlıkları (yüksek izleme değeri) ──
    public double Competitiveness { get; set; } = 1.0;
    public double GoalPotential { get; set; } = 1.0;
    public double Form { get; set; } = 0.7;
    public double H2HIntensity { get; set; } = 0.6;
    public double Confidence { get; set; } = 0.5;

    // ── Obscurity ağırlıkları (feed'de öne çıkmama) ──
    public double NotBigTeam { get; set; } = 1.0;   // 1 − TeamStrength
    public double LowVisibility { get; set; } = 1.0; // 1 − DiscoveryScore/100
    public double LowStakes { get; set; } = 0.6;     // 1 − Stakes
    public double NotPreferred { get; set; } = 0.6;  // 1 − kişiselleştirme

    // ── Eşikler ──
    public double MinHiddenGemScore { get; set; } = 45.0; // gem sayılması için min HiddenGemScore
    public double MinQuality { get; set; } = 0.55;         // "sadece düşük skor gem yapmaz" → kalite tabanı
    public double BigTeamHardCap { get; set; } = 0.75;     // TeamStrength ≥ bu → ASLA gem (büyük takım)
    public double InterestScale { get; set; } = 20.0;      // kişiselleştirme puanı normalizasyonu

    public static HiddenGemWeights Default => new();
}

/// <summary>
/// Bir maçın Hidden Gem analizi (bağımsız katman — sıralama değil, rozet değil). HiddenGemScore = kalite ×
/// obscurity. Gem yalnız GERÇEK sinyallerle + gerçekten radar-altı + büyük-takım-değil ise flag'lenir.
/// </summary>
public sealed record HiddenGemAnalysis
{
    public required int MatchId { get; init; }
    public bool IsHiddenGem { get; init; }
    public double HiddenGemScore { get; init; }  // 0-100 (kalite × obscurity)
    public double QualityScore { get; init; }    // 0-100
    public double Obscurity { get; init; }       // 0-1
    public required IReadOnlyList<string> Reasons { get; init; }
    public required IReadOnlyList<SignalContribution> Breakdown { get; init; }
}
