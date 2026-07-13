namespace Formax.Infrastructure.Historical.Features;

/// <summary>
/// Tek bir takımın, belirli bir maç TARİHİNDEN ÖNCEki geçmişinden türetilen özellikleri.
/// Tüm alanlar deterministiktir ve yalnızca maç öncesi bilinebilir veriden hesaplanır (leakage yok).
/// Veri yoksa (ör. şut istatistiği eksik) ilgili alan null.
/// </summary>
public sealed record TeamFeatures
{
    public double Last3Form { get; init; }
    public double Last5Form { get; init; }
    public double Last10Form { get; init; }
    public double WinRate { get; init; }
    public double HomeWinRate { get; init; }
    public double AwayWinRate { get; init; }
    public double GoalsScoredAverage { get; init; }
    public double GoalsConcededAverage { get; init; }
    public double GoalDifference { get; init; }
    public double BTTSRate { get; init; }
    public double Over25Rate { get; init; }
    public double CleanSheetRate { get; init; }
    public double FailedToScoreRate { get; init; }
    public int WinningStreak { get; init; }
    public int LosingStreak { get; init; }
    public int UnbeatenStreak { get; init; }
    public double HomeGoalsAverage { get; init; }
    public double AwayGoalsAverage { get; init; }
    public int? DaysSinceLastMatch { get; init; }
    public int? LeaguePosition { get; init; }
    public int LeaguePoints { get; init; }
    public double? ShotAccuracy { get; init; }
    public double? ShotDifference { get; init; }
    public double OffensiveRating { get; init; }
    public double DefensiveRating { get; init; }
    public double RecentMomentum { get; init; }
    public double? Elo { get; init; }

    /// <summary>Kaç geçmiş maç kullanıldı (0 ise "yeterli veri yok").</summary>
    public int SampleSize { get; init; }
}

/// <summary>
/// Bir maç için tam özellik vektörü: ev/deplasman takım özellikleri + maç-düzeyi diferansiyeller.
/// Yalnızca maç başlamadan önce bilinebilecek veriden üretilir.
/// </summary>
public sealed record MatchFeatureVector
{
    public int MatchId { get; init; }
    public required TeamFeatures Home { get; init; }
    public required TeamFeatures Away { get; init; }

    // ── Maç-düzeyi ──
    public double? EloDifference { get; init; }
    public double HomeAdvantage { get; init; }
    public double HeadToHeadWinRate { get; init; }
    public double HeadToHeadGoalsAverage { get; init; }
    public double LeagueStrength { get; init; }
    public int HeadToHeadSampleSize { get; init; }
}
