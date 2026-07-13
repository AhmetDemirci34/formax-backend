using Formax.Application.DTOs.Nabiz;
using Formax.Application.Interfaces;
using Formax.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Formax.Infrastructure.Nabiz;

/// <summary>
/// Fetches items from all configured RSS 2.0 / Atom sources.
///
/// Source list: read from appsettings.json → Nabiz:Sources.
/// Fallback: built-in defaults (BBC Sport, Sky Sports, ESPN Soccer) when no
/// sources are configured — ensures the engine works out of the box.
///
/// HTTP: uses IHttpClientFactory (short-lived client per source fetch).
/// Parse: XDocument (no extra NuGet dependency).
/// Errors: per-source failure is caught and logged; does not stop other sources.
/// </summary>
public class NabizRssFeedFetcher : INabizFeedFetcher
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<NabizRssFeedFetcher> _logger;
    private readonly List<NabizFeedSourceConfig> _sources;

    // ── Built-in defaults — active when appsettings Nabiz:Sources is empty ───

    private static readonly List<NabizFeedSourceConfig> DefaultSources = new()
    {
        new()
        {
            Name           = "BBC Sport",
            Url            = "https://feeds.bbci.co.uk/sport/football/rss.xml",
            SourceType     = NabizSourceType.News,
            AuthorVerified = true
        },
        new()
        {
            Name           = "Sky Sports Football",
            Url            = "https://www.skysports.com/rss/12040",
            SourceType     = NabizSourceType.Flash,
            AuthorVerified = true
        },
        new()
        {
            Name           = "ESPN Soccer",
            Url            = "https://www.espn.com/espn/rss/soccer/news",
            SourceType     = NabizSourceType.News,
            AuthorVerified = true
        },
        new()
        {
            Name           = "Goal.com",
            Url            = "https://www.goal.com/en/rss/news",
            SourceType     = NabizSourceType.Yorum,
            AuthorVerified = true
        }
    };

    public NabizRssFeedFetcher(
        IHttpClientFactory httpFactory,
        IOptions<NabizOptions> options,
        ILogger<NabizRssFeedFetcher> logger)
    {
        _httpFactory = httpFactory;
        _logger      = logger;
        _sources     = options.Value.Sources.Count > 0
                       ? options.Value.Sources
                       : DefaultSources;
    }

    /// <inheritdoc />
    public async Task<List<NabizRawItem>> FetchAllAsync(CancellationToken ct = default)
    {
        var tasks   = _sources.Select(s => FetchSourceAsync(s, ct));
        var results = await Task.WhenAll(tasks);

        var all = results.SelectMany(r => r).ToList();

        _logger.LogDebug(
            "[NABIZ RSS] fetched {Total} raw item(s) from {Sources} source(s)",
            all.Count, _sources.Count);

        return all;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Per-source fetch
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<List<NabizRawItem>> FetchSourceAsync(
        NabizFeedSourceConfig source,
        CancellationToken ct)
    {
        try
        {
            using var client = _httpFactory.CreateClient("NabizRss");
            var xml = await client.GetStringAsync(source.Url, ct);
            return ParseRss(xml, source);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[NABIZ RSS] fetch failed for source '{Source}' ({Url})",
                source.Name, source.Url);
            return new List<NabizRawItem>();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RSS 2.0 parser — no extra NuGet required
    // ──────────────────────────────────────────────────────────────────────────

    private List<NabizRawItem> ParseRss(string xml, NabizFeedSourceConfig source)
    {
        var items = new List<NabizRawItem>();

        try
        {
            var doc  = XDocument.Parse(xml);
            XNamespace media = "http://search.yahoo.com/mrss/";
            XNamespace dc    = "http://purl.org/dc/elements/1.1/";
            XNamespace atom  = "http://www.w3.org/2005/Atom";

            // ── Atom 1.0 path (root = <feed xmlns="...Atom">, items = <entry>) ──
            // PATCH: previously only <item> was iterated; pure Atom feeds returned
            // 0 results silently. Detect by root local name or default namespace.
            var isAtom = doc.Root?.Name.LocalName == "feed" &&
                         doc.Root.Name.Namespace  == atom;

            if (isAtom)
            {
                foreach (var entry in doc.Root!.Elements(atom + "entry"))
                {
                    var title  = entry.Element(atom + "title")?.Value?.Trim() ?? string.Empty;

                    // Atom <link> is a self-closing element with href attribute.
                    // Skip rel="self" links — prefer rel="alternate" (the article URL).
                    var link = entry.Elements(atom + "link")
                                    .FirstOrDefault(l => l.Attribute("rel")?.Value != "self")
                                    ?.Attribute("href")?.Value?.Trim()
                            ?? entry.Element(atom + "link")?.Attribute("href")?.Value?.Trim()
                            ?? string.Empty;

                    var desc   = StripHtml(
                                     entry.Element(atom + "summary")?.Value
                                  ?? entry.Element(atom + "content")?.Value
                                  ?? string.Empty);

                    // Atom <author> wraps <name> as a child element
                    var author = entry.Element(atom + "author")?.Element(atom + "name")?.Value?.Trim()
                              ?? source.Name;

                    var pubRaw = entry.Element(atom + "published")?.Value
                              ?? entry.Element(atom + "updated")?.Value;

                    var image  = entry.Element(media + "content")?.Attribute("url")?.Value
                              ?? entry.Element(media + "thumbnail")?.Attribute("url")?.Value;

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
                        continue;

                    DateTime pubDate = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(pubRaw))
                        _ = DateTime.TryParse(pubRaw, out pubDate);

                    if ((DateTime.UtcNow - pubDate).TotalHours > 48) continue;

                    var summary  = desc.Length > 500 ? desc[..500] : desc;
                    var keywords = ExtractKeywords(title + " " + summary);

                    items.Add(new NabizRawItem
                    {
                        Source         = source.Name,
                        SourceType     = source.SourceType,
                        Author         = author,
                        AuthorVerified = source.AuthorVerified,
                        Headline       = title,
                        Summary        = summary,
                        ImageUrl       = image,
                        SourceUrl      = link,
                        PublishedAt    = pubDate,
                        Keywords       = keywords
                    });
                }

                return items;
            }

            // ── RSS 2.0 path (root = <rss>, items = <item> under <channel>) ────
            // Also handles channel-root feeds (root IS the channel element).
            var channel = doc.Root?.Element("channel") ?? doc.Root;
            if (channel == null) return items;

            foreach (var item in channel.Elements("item"))
            {
                var title    = item.Element("title")?.Value?.Trim() ?? string.Empty;
                var link     = item.Element("link")?.Value?.Trim()
                            ?? item.Element(atom + "link")?.Attribute("href")?.Value?.Trim()
                            ?? string.Empty;
                var desc     = StripHtml(item.Element("description")?.Value ?? string.Empty);
                var author   = item.Element("author")?.Value?.Trim()
                            ?? item.Element(dc + "creator")?.Value?.Trim()
                            ?? source.Name;
                var pubRaw   = item.Element("pubDate")?.Value
                            ?? item.Element(atom + "published")?.Value;

                // Image: enclosure → media:content → media:thumbnail
                var image = item.Element("enclosure")?.Attribute("url")?.Value
                         ?? item.Element(media + "content")?.Attribute("url")?.Value
                         ?? item.Element(media + "thumbnail")?.Attribute("url")?.Value;

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
                    continue;

                DateTime pubDate = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(pubRaw))
                    _ = DateTime.TryParse(pubRaw, out pubDate);

                // Ignore items older than 48 h at fetch time
                if ((DateTime.UtcNow - pubDate).TotalHours > 48)
                    continue;

                var summary  = desc.Length > 500 ? desc[..500] : desc;
                var keywords = ExtractKeywords(title + " " + summary);

                items.Add(new NabizRawItem
                {
                    Source         = source.Name,
                    SourceType     = source.SourceType,
                    Author         = author,
                    AuthorVerified = source.AuthorVerified,
                    Headline       = title,
                    Summary        = summary,
                    ImageUrl       = image,
                    SourceUrl      = link,
                    PublishedAt    = pubDate,
                    Keywords       = keywords
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[NABIZ RSS] parse failed for source '{Source}'", source.Name);
        }

        return items;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        return Regex.Replace(html, "<[^>]+>", " ")
            .Replace("&nbsp;",  " ")
            .Replace("&amp;",   "&")
            .Replace("&lt;",    "<")
            .Replace("&gt;",    ">")
            .Replace("&quot;",  "\"")
            .Replace("&#39;",   "'")
            .Replace("&#8220;", "\"")
            .Replace("&#8221;", "\"")
            .Replace("&#8230;", "...")
            .Trim();
    }

    private static readonly HashSet<string> StopWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "that", "with", "this", "from", "have", "will", "they", "their",
            "been", "were", "what", "when", "which", "into", "over", "after",
            "also", "said", "more", "than", "then", "some", "time", "year",
            "news", "sport", "football", "soccer", "match", "game", "team",
            "club", "player", "season", "league", "week", "last", "next"
        };

    private static List<string> ExtractKeywords(string text)
        => Regex.Matches(text.ToLowerInvariant(), @"\b[a-zğüşıöçÇÖĞÜŞİ]{4,}\b")
                .Select(m => m.Value)
                .Where(w => !StopWords.Contains(w))
                .Distinct()
                .Take(40)
                .ToList();
}
