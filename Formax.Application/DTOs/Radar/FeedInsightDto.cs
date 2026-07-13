namespace Formax.Application.DTOs.Radar
{
    /// <summary>
    /// Radar Feed (R.13.2) — API-facing projection of a feed insight. Hidden matches are
    /// never included; the list is importance-ordered by the query service.
    /// </summary>
    public sealed class FeedInsightDto
    {
        public int MatchId { get; init; }
        public string Headline { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public double ImportanceScore { get; init; }
        public string PrimarySignal { get; init; } = string.Empty;

        /// <summary>Commentary tone as a string (enum name).</summary>
        public string CommentaryTone { get; init; } = string.Empty;

        /// <summary>Commentary visibility as a string (enum name); never "Hidden".</summary>
        public string Visibility { get; init; } = string.Empty;
    }
}
