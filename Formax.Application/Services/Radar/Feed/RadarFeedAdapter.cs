using Formax.Application.DTOs.Radar;
using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.3) — default adapter. Copies the Radar insight's headline/summary/
    /// signal/tone onto feed card shapes. Ranking scores on the recommendation card are
    /// left untouched (no ranking/learning in scope).
    /// </summary>
    public sealed class RadarFeedAdapter : IRadarFeedAdapter
    {
        public FeedCardModel ToCard(FeedInsightDto insight)
            => new()
            {
                MatchId = insight.MatchId,
                Headline = insight.Headline,
                Summary = insight.Summary,
                ImportanceScore = insight.ImportanceScore,
                PrimarySignal = insight.PrimarySignal,
                CommentaryTone = insight.CommentaryTone
            };

        public void Apply(RecommendationCardDto card, FeedInsightDto insight)
        {
            // Radar-fed content fields only — scores (Score/RecommendationScore/...) untouched.
            card.StoryHeadline = insight.Headline;
            card.StoryBody = insight.Summary;
            card.AiSummary = insight.Summary;
            card.InsightLabel = insight.PrimarySignal;
            card.InsightReason = insight.Headline;
        }
    }
}
