using System.Collections.Generic;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.DailyPicks;

/// <summary>Günün seçimlerinden biri — bir maça referans + kategori + ilgili metrik + gerekçe. Sıralama/skor değiştirmez.</summary>
public sealed record DailyPick
{
    public required int MatchId { get; init; }
    /// <summary>Kategori: TodaysBest / Top5 / HiddenGem / HighConfidence / GoalMatch / TightMatch.</summary>
    public required string Category { get; init; }
    /// <summary>Bu kategoriyi belirleyen metrik değeri (ör. confidence, gol potansiyeli, discovery skoru).</summary>
    public double Score { get; init; }
    public required string Reason { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = new List<string>();
    public bool HiddenGem { get; init; }
    public double DiscoveryScore { get; init; }
    public double PersonalScore { get; init; }
    public double RecommendationScore { get; init; }
}

/// <summary>Günün en iyi maç listesi. Discovery/Recommendation/Radar'ı DEĞİŞTİRMEZ; yalnız seçer.</summary>
public sealed record DailyPicksResult
{
    public DailyPick? TodaysBestMatch { get; init; }
    public required IReadOnlyList<DailyPick> Top5 { get; init; }
    public DailyPick? BestHiddenGem { get; init; }
    public DailyPick? BestHighConfidenceMatch { get; init; }
    public DailyPick? BestGoalMatch { get; init; }
    public DailyPick? BestTightMatch { get; init; }
}
