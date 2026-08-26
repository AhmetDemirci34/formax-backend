using System.Collections.Generic;

namespace Formax.Infrastructure.Historical.Prediction.Ranking;

/// <summary>Radar Score bandı — "bugün bu maçı izlemeli miyim?" cevabının kabaca seviyesi.</summary>
public enum RadarLevel
{
    Low,
    Medium,
    High,
    MustWatch
}

/// <summary>
/// Tek bir sinyalin skora katkısı (sıralamanın açıklanabilirliği). Value = ham normalize sinyal [0,1];
/// WeightedContribution = bu sinyalin nihai skora kattığı puan. Veri yoksa DataAvailable=false + Note.
/// </summary>
public sealed record SignalContribution
{
    public required string Signal { get; init; }
    /// <summary>Ham normalize sinyal değeri [0,1] (confidence-kapısı öncesi).</summary>
    public double Value { get; init; }
    public double Weight { get; init; }
    /// <summary>Nihai skora kattığı puan (tüm katkılar + hidden gem = Radar Score).</summary>
    public double WeightedContribution { get; init; }
    public bool DataAvailable { get; init; } = true;
    public string? Note { get; init; }
}

/// <summary>
/// Bir maçın Radar Score sonucu: 0-100 skor + bant + hidden gem rozeti + tam breakdown. Breakdown'daki tüm
/// WeightedContribution değerlerinin toplamı = Score (değişmez).
/// </summary>
public sealed record RadarScore
{
    public required int MatchId { get; init; }
    public double Score { get; init; }
    public RadarLevel Level { get; init; }
    public bool HiddenGem { get; init; }
    public double HiddenGemBonusApplied { get; init; }
    public required IReadOnlyList<SignalContribution> Breakdown { get; init; }
}
