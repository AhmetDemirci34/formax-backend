using System.Collections.Generic;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Personalization;

/// <summary>
/// Kişisel Radar Score: Base Radar Score + User Interest = Personal Radar Score. Base HİÇ değişmez (ayrı alanda
/// korunur); User Interest yalnız ek katmandır. Breakdown, base'in tüm sinyalleri + "UserInterest" satırını
/// içerir; tüm WeightedContribution toplamı = PersonalScore.
/// </summary>
public sealed record PersonalRadarScore
{
    public required int MatchId { get; init; }
    public int UserId { get; init; }

    /// <summary>Değişmeyen objektif Base Radar Score (0-100).</summary>
    public double BaseScore { get; init; }
    /// <summary>Kişiselleştirilmiş skor = Base + uygulanan User Interest (0-100).</summary>
    public double PersonalScore { get; init; }

    public RadarLevel Level { get; init; }

    /// <summary>Bu kullanıcı için kişiselleştirme uygulandı mı (veri yoksa false → PersonalScore = BaseScore).</summary>
    public bool Personalized { get; init; }
    /// <summary>User Interest'in eklediği puan (PersonalScore − BaseScore).</summary>
    public double UserInterestApplied { get; init; }

    public bool HiddenGem { get; init; }

    public required IReadOnlyList<SignalContribution> Breakdown { get; init; }
}
