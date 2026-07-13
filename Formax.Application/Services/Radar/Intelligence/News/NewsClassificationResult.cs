using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.2) — the single category a news item received and
    /// the keyword that decided it. Deterministic.
    /// </summary>
    public sealed class NewsClassificationResult
    {
        public NewsCategory Category { get; init; }

        /// <summary>The keyword that matched, or empty for the General fallback.</summary>
        public string MatchedKeyword { get; init; } = string.Empty;

        public static NewsClassificationResult Of(NewsCategory category, string keyword)
            => new() { Category = category, MatchedKeyword = keyword };
    }
}
