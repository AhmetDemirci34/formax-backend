namespace Formax.Infrastructure.Historical.Prediction.Ranking;

/// <summary>
/// Radar Score ağırlıkları + çarpanları — TAMAMEN config-driven POCO (motor bunları parametre olarak alır, sabit
/// kodlamaz). Host, config bölümünden (ör. "ProbabilityEngine:RadarWeights") bağlayarak override edebilir.
/// Varsayılan = onaylı "Dengeli" profil. Deterministik.
/// </summary>
public sealed class RadarWeights
{
    // ── Sinyal ağırlıkları (Dengeli profil) ──
    public double Competitiveness { get; set; } = 1.0;
    public double GoalPotential { get; set; } = 1.0;
    public double Form { get; set; } = 0.9;
    public double Stakes { get; set; } = 0.9;
    public double TeamStrength { get; set; } = 0.8;
    public double PowerBalance { get; set; } = 0.7;
    public double H2HIntensity { get; set; } = 0.7;
    public double Upset { get; set; } = 0.6;
    public double LeagueQuality { get; set; } = 0.6;
    /// <summary>Derby ağırlığı — veri geldiğinde devreye girer (şu an rivalry verisi yok → nötr).</summary>
    public double Derby { get; set; } = 0.8;

    // ── Çarpanlar ──
    /// <summary>
    /// Confidence güvenilirlik kapısının gücü [0,1]. reliability = 1 − ConfidenceMultiplier·(1 − ConfidenceScore).
    /// 0 = kapı kapalı (confidence etkisiz), 1 = tam kapı (reliability = ConfidenceScore). Olasılık-türevli
    /// sinyalleri (Competitiveness, Upset) ölçekler. Confidence ASLA doğrudan puan değildir.
    /// </summary>
    public double ConfidenceMultiplier { get; set; } = 0.5;

    /// <summary>Hidden Gem tetiklendiğinde eklenecek maksimum bonus puan (0-100 skalasında).</summary>
    public double HiddenGemBonus { get; set; } = 8.0;

    /// <summary>Hidden Gem eşiği: içsel kalite (rekabet+gol+form ort.) bunun üstündeyse aday.</summary>
    public double HiddenGemQualityThreshold { get; set; } = 0.60;

    /// <summary>Hidden Gem tavanı: Stakes bunun altındaysa "radar-altı" sayılır (düşük dikkat).</summary>
    public double HiddenGemStakesCeiling { get; set; } = 0.40;

    /// <summary>Onaylı Dengeli varsayılan profil.</summary>
    public static RadarWeights Balanced => new();
}
