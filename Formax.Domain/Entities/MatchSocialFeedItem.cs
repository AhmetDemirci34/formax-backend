using Formax.Domain.Enums;

namespace Formax.Domain.Entities;

/// <summary>
/// A single NABIZ feed item — a news article, press quote, club announcement,
/// or editorial commentary that has been linked to a specific match and/or team.
///
/// One row per unique article (ContentHash is globally unique).
/// MatchId is set to the highest-relevance match found; null when no match
/// in the current window matched the item's content.
/// </summary>
public class MatchSocialFeedItem
{
    public int Id { get; set; }

    /// <summary>
    /// The match this item is most relevant to.
    /// Null when the item could not be linked to any active match.
    /// </summary>
    public int? MatchId { get; set; }

    /// <summary>
    /// Direct team association (Phase B: club-specific official feeds).
    /// Null in Phase A.
    /// </summary>
    public int? TeamId { get; set; }

    /// <summary>Human-readable source name, e.g. "BBC Sport", "Sky Sports".</summary>
    public string Source { get; set; } = string.Empty;

    public NabizSourceType SourceType { get; set; } = NabizSourceType.News;

    /// <summary>Byline author or feed source name when no author is available.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>True for verified publisher accounts / major outlets.</summary>
    public bool AuthorVerified { get; set; }

    public string Headline { get; set; } = string.Empty;

    /// <summary>Short excerpt or lead paragraph, max 500 chars.</summary>
    public string Summary { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    /// <summary>Canonical URL of the original article / post.</summary>
    public string SourceUrl { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Sentiment score in [-1.0, 1.0]. Negative = critical, positive = positive.
    /// Null in Phase A (architecture hook for Phase B ML layer).
    /// </summary>
    public double? SentimentScore { get; set; }

    /// <summary>
    /// How relevant this item is to its associated match. Range: [0.0, 1.0].
    /// Computed by NabizRelevanceEngine; used for feed ranking.
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// SHA-256 hex of (SourceUrl | lower(Headline)).
    /// Unique index in DB — prevents duplicate stories entering the feed.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
