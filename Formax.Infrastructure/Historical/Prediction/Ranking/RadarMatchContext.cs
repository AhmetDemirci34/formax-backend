using Formax.Infrastructure.Historical.Prediction.Confidence;
using Formax.Infrastructure.Historical.Prediction.Ranking.Discovery;

namespace Formax.Infrastructure.Historical.Prediction.Ranking;

/// <summary>
/// Bir maçın tüm Radar katmanı çıktılarını taşıyan PAYLAŞILAN bağlam (Hidden Gems / Daily Picks / Radar API
/// bunu tüketir). MEVCUT katmanların çıktılarını değiştirmeden bir araya getirir: Discovery item + Base Radar
/// Score breakdown + olasılık + confidence. Üst katmanlar yalnız OKUR.
/// </summary>
public sealed record RadarMatchContext
{
    public required int MatchId { get; init; }
    public required DiscoveryFeedItem Discovery { get; init; }
    public required RadarScore Radar { get; init; }
    public required double[] Probability { get; init; }
    public required ConfidenceAssessment Confidence { get; init; }
}
