namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.3) — the fields an existing FORMAX feed card needs from Radar.
    /// The integration target the adapter maps a <c>FeedInsightDto</c> onto, so the Home
    /// feed can consume Radar output without new ranking/learning/intelligence.
    /// </summary>
    public sealed class FeedCardModel
    {
        public int MatchId { get; init; }
        public string Headline { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public double ImportanceScore { get; init; }
        public string PrimarySignal { get; init; } = string.Empty;
        public string CommentaryTone { get; init; } = string.Empty;
    }
}
