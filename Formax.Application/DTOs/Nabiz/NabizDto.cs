using Formax.Domain.Enums;

namespace Formax.Application.DTOs.Nabiz;

// ── Internal pipeline model (adapter → ingestion job) ────────────────────────

/// <summary>
/// Raw item produced by a NabizFeedFetcher before persistence.
/// Contains both content and keyword tokens used by NabizRelevanceEngine.
/// </summary>
public class NabizRawItem
{
    public string Source { get; set; } = string.Empty;
    public NabizSourceType SourceType { get; set; } = NabizSourceType.News;
    public string Author { get; set; } = string.Empty;
    public bool AuthorVerified { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Lowercase tokens extracted from headline + summary.
    /// Passed to NabizRelevanceEngine for keyword-overlap scoring.
    /// </summary>
    public List<string> Keywords { get; set; } = new();
}

// ── Frontend DTOs ─────────────────────────────────────────────────────────────

public class NabizFeedItemDto
{
    /// <summary>NabizSourceType enum name: "Flash" | "Yorum" | "Roportaj" | "Official" | "Trend" | "News"</summary>
    public string Type { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool AuthorVerified { get; set; }
    public string Headline { get; set; } = string.Empty;

    /// <summary>Short excerpt. Null when provider supplied no description.</summary>
    public string? Summary { get; set; }

    public string? ImageUrl { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
}

/// <summary>
/// Top-level NABIZ section attached to MatchDetailDto.
/// Returns empty Items list cleanly when no content is available — no nulls, no placeholders.
/// </summary>
public class NabizSectionDto
{
    public List<NabizFeedItemDto> Items { get; set; } = new();

    /// <summary>Convenience flag for the frontend — no need to check Items.Count.</summary>
    public bool HasContent => Items.Count > 0;
}
