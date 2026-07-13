using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Formax.Application.Services.News.Discovery;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.News.Providers
{
    /// <summary>
    /// FORMAX Data Engine v2 — Google News RSS provider (keyless, çok-yayıncı).
    /// Tek sorgu onlarca yayıncıyı (ESPN, Guardian, Sky…) toplar → doğal çok-kaynak.
    /// Hata → boş liste; tek sorgu başarısız olsa diğerleri sürer.
    /// </summary>
    public sealed class GoogleNewsRssProvider : INewsProvider
    {
        private const int MaxQueries = 5;     // maç başına çalıştırılacak sorgu sayısı
        private const int MaxPerQuery = 15;
        private const int FreshnessDays = 14;

        private readonly HttpClient _http;
        private readonly ILogger<GoogleNewsRssProvider> _logger;

        public GoogleNewsRssProvider(HttpClient http, ILogger<GoogleNewsRssProvider> logger)
        {
            _http = http;
            _logger = logger;
            if (_http.Timeout == default || _http.Timeout.TotalSeconds > 20)
                _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public string Name => "Google News";
        public bool IsEnabled => true;

        public async Task<IReadOnlyList<NewsCandidate>> SearchAsync(NewsQuery query, CancellationToken ct = default)
        {
            var results = new List<NewsCandidate>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cutoff = DateTime.UtcNow.AddDays(-FreshnessDays);

            foreach (var q in query.Queries.Take(MaxQueries))
            {
                ct.ThrowIfCancellationRequested();
                var url = $"https://news.google.com/rss/search?q={WebUtility.UrlEncode(q)}&hl=en-US&gl=US&ceid=US:en";
                try
                {
                    var xml = await _http.GetStringAsync(url, ct);
                    Parse(xml, query, cutoff, seenUrls, results);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NEWS/Google] sorgu başarısız: {Q}", q);
                }
            }

            return results;
        }

        private void Parse(string xml, NewsQuery query, DateTime cutoff,
            HashSet<string> seenUrls, List<NewsCandidate> sink)
        {
            XDocument doc;
            try { doc = XDocument.Parse(xml); } catch { return; }

            var channel = doc.Root?.Element("channel");
            if (channel == null) return;

            var taken = 0;
            foreach (var item in channel.Elements("item"))
            {
                if (taken >= MaxPerQuery) break;

                var rawTitle = item.Element("title")?.Value?.Trim() ?? "";
                var link = item.Element("link")?.Value?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(rawTitle) || string.IsNullOrWhiteSpace(link)) continue;
                if (!seenUrls.Add(link)) continue;

                var (headline, publisher) = SplitPublisher(rawTitle);

                var pubRaw = item.Element("pubDate")?.Value;
                var pub = DateTime.TryParse(pubRaw, out var p) ? p.ToUniversalTime() : DateTime.UtcNow;
                if (pub < cutoff) continue;

                sink.Add(new NewsCandidate
                {
                    Headline = headline,
                    Summary = StripTags(item.Element("description")?.Value ?? ""),
                    Url = link,
                    PublishedUtc = pub,
                    Provider = Name,
                    Publisher = publisher,
                    Language = "en",
                    League = query.League,
                    Country = query.Country,
                    FormaxMatchId = query.FormaxMatchId
                });
                taken++;
            }
        }

        // "Headline - Publisher" → (headline, publisher)
        private static (string Headline, string Publisher) SplitPublisher(string title)
        {
            var idx = title.LastIndexOf(" - ", StringComparison.Ordinal);
            return idx > 10 ? (title[..idx].Trim(), title[(idx + 3)..].Trim()) : (title, "");
        }

        private static string StripTags(string html) =>
            System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ").Trim();
    }
}
