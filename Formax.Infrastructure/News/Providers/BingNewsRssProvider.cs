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
    /// FORMAX Data Engine v2 — Bing News RSS provider (keyless, ikincil açık kaynak).
    /// Google News ile aynı maçı doğrularsa çok-kaynak güveni yükselir. Hata → boş liste.
    /// </summary>
    public sealed class BingNewsRssProvider : INewsProvider
    {
        private const int MaxQueries = 3;
        private const int MaxPerQuery = 15;
        private const int FreshnessDays = 14;

        private readonly HttpClient _http;
        private readonly ILogger<BingNewsRssProvider> _logger;

        public BingNewsRssProvider(HttpClient http, ILogger<BingNewsRssProvider> logger)
        {
            _http = http;
            _logger = logger;
            if (_http.Timeout == default || _http.Timeout.TotalSeconds > 20)
                _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public string Name => "Bing News";
        public bool IsEnabled => true;

        public async Task<IReadOnlyList<NewsCandidate>> SearchAsync(NewsQuery query, CancellationToken ct = default)
        {
            var results = new List<NewsCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cutoff = DateTime.UtcNow.AddDays(-FreshnessDays);

            foreach (var q in query.Queries.Take(MaxQueries))
            {
                ct.ThrowIfCancellationRequested();
                var url = $"https://www.bing.com/news/search?q={WebUtility.UrlEncode(q)}&format=rss";
                try
                {
                    var xml = await _http.GetStringAsync(url, ct);
                    Parse(xml, query, cutoff, seen, results);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NEWS/Bing] sorgu başarısız: {Q}", q);
                }
            }

            return results;
        }

        private void Parse(string xml, NewsQuery query, DateTime cutoff,
            HashSet<string> seen, List<NewsCandidate> sink)
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
                if (pub < cutoff) continue;

                sink.Add(new NewsCandidate
                {
                    Headline = title,
                    Summary = StripTags(item.Element("description")?.Value ?? ""),
                    Url = link,
                    PublishedUtc = pub,
                    Provider = Name,
                    Publisher = Host(link),
                    Language = "en",
                    League = query.League,
                    Country = query.Country,
                    FormaxMatchId = query.FormaxMatchId
                });
                taken++;
            }
        }

        private static string Host(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : "";

        private static string StripTags(string html) =>
            System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ").Trim();
    }
}
