using Formax.Application.DTOs.Radar;
using Formax.Application.DTOs.Recommendations;

namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.3) — maps a Radar <see cref="FeedInsightDto"/> onto feed card
    /// shapes so the existing Home/Recommendation feed can consume Radar output. Pure
    /// mapping: no ranking, no scoring change, no new intelligence.
    /// </summary>
    public interface IRadarFeedAdapter
    {
        /// <summary>Map a Radar insight to the lightweight feed card model.</summary>
        FeedCardModel ToCard(FeedInsightDto insight);

        /// <summary>Populate an existing recommendation card's Radar-fed fields
        /// (headline/summary/insight labels) from a Radar insight, without touching
        /// any ranking scores.</summary>
        void Apply(RecommendationCardDto card, FeedInsightDto insight);
    }
}
