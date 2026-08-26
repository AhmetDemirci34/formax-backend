namespace Formax.Infrastructure.Historical.Prediction.Ranking.Personalization;

/// <summary>
/// Kişiselleştirme katmanı config'i — TAMAMEN config-driven POCO. User Interest Bonus + decay yarı-ömrü buradan
/// yönetilir. Base Radar Score'a DOKUNMAZ; yalnız kişisel katmanın davranışını belirler. Deterministik.
/// </summary>
public sealed class PersonalRadarWeights
{
    /// <summary>Tam ilgi (affinity 100) + taze (decay 1.0) durumunda base'e eklenecek maksimum puan.</summary>
    public double UserInterestBonus { get; set; } = 25.0;

    /// <summary>Interest Decay yarı-ömrü (gün): ilgi her bu kadar günde yarıya iner. Büyük = yavaş unutma.</summary>
    public double InterestDecayHalfLifeDays { get; set; } = 30.0;

    /// <summary>Bu affinity'nin altındaki ilgi "veri yok" sayılır (base ile çalışılır).</summary>
    public int MinAffinityToApply { get; set; } = 1;

    public static PersonalRadarWeights Default => new();
}
