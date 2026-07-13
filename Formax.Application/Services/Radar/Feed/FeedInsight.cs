using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Feed
{
    /// <summary>
    /// Radar Feed (R.13.1) — the Radar-fed view model for a feed card. Assembled from the
    /// match intelligence, news, and commentary snapshots. Read model only: no new
    /// intelligence, no scoring, no persistence. Hidden-commentary matches are excluded
    /// by the builder, so every FeedInsight is a meaningful match.
    /// </summary>
    public sealed class FeedInsight
    {
        public int MatchId { get; init; }

        /// <summary>From commentary headline.</summary>
        public string Headline { get; init; } = string.Empty;

        /// <summary>From commentary summary.</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>From match intelligence importance score.</summary>
        public double ImportanceScore { get; init; }

        /// <summary>From match intelligence primary signal (type name).</summary>
        public string PrimarySignal { get; init; } = string.Empty;

        /// <summary>From commentary tone.</summary>
        public CommentaryTone CommentaryTone { get; init; }

        /// <summary>From commentary visibility (Short or Full; never Hidden here).</summary>
        public CommentaryVisibility Visibility { get; init; }
    }
}
