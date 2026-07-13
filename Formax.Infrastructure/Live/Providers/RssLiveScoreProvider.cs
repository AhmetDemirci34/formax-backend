using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Formax.Application.Services.Live.Discovery;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Live.Providers
{
    /// <summary>
    /// FORMAX Live Data Engine — keyless RSS canlı-skor provider'ı. Maç penceresinde
    /// açık haber RSS'lerini (Google News + Bing News) canlı-skor sorgularıyla tarar;
    /// taze başlıklar skoru ("Home 2-1 Away") ve yayıncıyı taşır. Tek üçüncü-taraf API'ye
    /// bağımlı değildir; hata → boş liste. (BingNewsRssProvider ile aynı desen.)
    /// </summary>
    public sealed class RssLiveScoreProvider : ILiveSignalProvider
    {
        private const int MaxQueries = 3;
        private const int MaxPerQuery = 12;

        private readonly HttpClient _http;
        private readonly ILogger<RssLiveScoreProvider> _logger;

        public RssLiveScoreProvider(HttpClient http, ILogger<RssLiveScoreProvider> logger)
        {
            _http = http;
            _logger = logger;
            if (_http.Timeout == default || _http.Timeout.TotalSeconds > 20)
                _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public string Name => "RSS Live";
        public bool IsEnabled => true;

        public async Task<IReadOnlyList<LiveSignalCandidate>> FetchAsync(
            LiveSignalQuery query, CancellationToken ct = default)
        {
            var results = new List<LiveSignalCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var q in Truncate(query.Queries, MaxQueries))
            {
                ct.ThrowIfCancellationRequested();
                foreach (var url in FeedUrls(q))
                {
                    try
                    {
                        var xml = await _http.GetStringAsync(url, ct);
                        Parse(xml, seen, results);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[LIVE DISC/RSS] sorgu başarısız: {Q}", q);
                    }
                }
            }

            return results;
        }

        private static IEnumerable<string> Truncate(IReadOnlyList<string> items, int max)
        {
            var n = Math.Min(max, items.Count);
            for (var i = 0; i < n; i++) yield return items[i];
        }

        private static IEnumerable<string> FeedUrls(string q)
        {
            var enc = WebUtility.UrlEncode(q);
            // İki bağımsız açık RSS motoru → çok-yayıncı doğrulaması.
            yield return $"https://news.google.com/rss/search?q={enc}&hl=en-US&gl=US&ceid=US:en";
            yield return $"https://www.bing.com/news/search?q={enc}&format=rss";
        }

        private void Parse(string xml, HashSet<string> seen, List<LiveSignalCandidate> sink)
        {
            XDocument doc;
            try { doc = XDocument.Parse(xml); } catch { return; }

            var channel = doc.Root?.Element("channel");
            if (channel == null) return;

            var taken = 0;
            foreach (var item in channel.Elements("item"))
            {
                if (taken >= MaxPerQuery) break;

                var title = item.Element("title")?.Value?.Trim() ?? "";
                var link = item.Element("link")?.Value?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link)) continue;
                if (!seen.Add(link)) continue;

                var pubRaw = item.Element("pubDate")?.Value;
                var pub = DateTime.TryParse(pubRaw, out var p) ? p.ToUniversalTime() : DateTime.UtcNow;

                sink.Add(new LiveSignalCandidate
                {
                    Provider = Name,
                    Publisher = ResolvePublisher(item, link),
                    Headline = title,
                    Summary = StripTags(item.Element("description")?.Value ?? ""),
                    Url = link,
                    PublishedUtc = pub
                });
                taken++;
            }
        }

        // Google News RSS <source>, Bing yoksa link host'u.
        private static string ResolvePublisher(XElement item, string link)
        {
            var src = item.Element("source")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(src)) return src;
            return Uri.TryCreate(link, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : "";
        }

        private static string StripTags(string html) =>
            System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ").Trim();
    }
}
