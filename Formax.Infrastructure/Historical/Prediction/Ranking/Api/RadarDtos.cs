using System.Collections.Generic;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Api;

/// <summary>Olasılık dağılımı [H/D/A].</summary>
public sealed record ProbabilityDto
{
    public double HomeWin { get; init; }
    public double Draw { get; init; }
    public double AwayWin { get; init; }
}

/// <summary>Recommendation özeti (API response için).</summary>
public sealed record RecommendationSummaryDto
{
    public double Score { get; init; }
    public required string Level { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
}

/// <summary>
/// Radar API'nin zengin per-maç yanıtı: RadarScore + DiscoveryScore + Recommendation + HiddenGem + Breakdown +
/// Confidence + Probability. Tüm alt katman çıktılarını (değiştirmeden) tek yerde birleştirir.
/// </summary>
public sealed record RadarMatchDto
{
    public int MatchId { get; init; }
    public double RadarScore { get; init; }        // Base Radar Score
    public double PersonalScore { get; init; }
    public double DiscoveryScore { get; init; }
    public int DiscoveryRank { get; init; }

    public bool HiddenGem { get; init; }
    public double HiddenGemScore { get; init; }

    public double ConfidenceScore { get; init; }
    public required string ConfidenceLevel { get; init; }
    public required ProbabilityDto Probability { get; init; }

    public required RecommendationSummaryDto Recommendation { get; init; }
    public required IReadOnlyList<SignalContribution> Breakdown { get; init; } // Radar breakdown
}
