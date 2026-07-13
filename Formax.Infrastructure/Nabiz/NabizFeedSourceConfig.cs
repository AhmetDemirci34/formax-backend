using Formax.Domain.Enums;

namespace Formax.Infrastructure.Nabiz;

/// <summary>
/// Configuration record for a single RSS/Atom news source.
/// Loaded from appsettings.json → Nabiz:Sources[].
/// </summary>
public class NabizFeedSourceConfig
{
    /// <summary>Human-readable label, e.g. "BBC Sport".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full RSS 2.0 / Atom feed URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Editorial type assigned to all items from this source.</summary>
    public NabizSourceType SourceType { get; set; } = NabizSourceType.News;

    /// <summary>
    /// True for established media outlets — shown as a verified badge in the UI.
    /// </summary>
    public bool AuthorVerified { get; set; }
}

/// <summary>
/// Bound from appsettings.json "Nabiz" section.
/// When Sources is empty, NabizRssFeedFetcher falls back to built-in defaults.
/// </summary>
public class NabizOptions
{
    public List<NabizFeedSourceConfig> Sources { get; set; } = new();
}
