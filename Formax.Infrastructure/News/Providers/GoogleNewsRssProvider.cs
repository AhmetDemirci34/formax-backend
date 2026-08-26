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
        private const int DefaultQueries = 5;   // maç başına çalıştırılacak sorgu sayısı
        private const int MaxQueriesHardCap = 8;
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

            var budget = query.QueryBudget > 0
                ? Math.Min(query.QueryBudget, MaxQueriesHardCap)
                : DefaultQueries;

            // Dil/bölge: Türk takımlarının gerçek kadro/sakatlık haberi Türkçe yayıncılarda
            // çıkar; en-US ile aranınca bu kaynaklar hiç görünmüyordu.
            var (hl, gl, ceid) = query.Locale == "tr"
                ? ("tr", "TR", "TR:tr")
                : ("en-US", "US", "US:en");

            foreach (var q in query.Queries.Take(budget))
            {
                ct.ThrowIfCancellationRequested();
                var url = $"https://news.google.com/rss/search?q={WebUtility.UrlEncode(q)}&hl={hl}&gl={gl}&ceid={ceid}";
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

                // Yayıncı adı <source> elementinde de bulunur; başlık ayrıştırması
                // başarısızsa (başlıkta " - " yoksa) gerçek yayıncı buradan alınır.
                if (string.IsNullOrWhiteSpace(publisher))
                    publisher = item.Element("source")?.Value?.Trim() ?? "";

                sink.Add(new NewsCandidate
                {
                    Headline = headline,
                    Summary = RealSummary(item.Element("description")?.Value, headline, publisher),
                    Url = link,
                    PublishedUtc = pub,
                    Provider = Name,
                    Publisher = publisher,
                    Language = query.Locale == "tr" ? "tr" : "en",
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

        /// <summary>
        /// GERÇEK ÖZET Mİ, BAŞLIĞIN YANKISI MI? Google News RSS'in &lt;description&gt;'ı çoğu
        /// zaman gerçek bir snippet DEĞİLDİR: linklenmiş başlık + yayıncı adıdır
        /// ("&lt;a…&gt;Başlık&lt;/a&gt;&amp;nbsp;&amp;nbsp;ESPN"). Böyle bir metni "haber içeriği" diye
        /// taşımak, başlığı özet gibi göstermek olur — YAPILMAZ. Sahte özet üretilmez;
        /// gerçek içerik yoksa alan BOŞ kalır ve haber yalnız başlık olarak değerlendirilir.
        /// </summary>
        private static string RealSummary(string? description, string headline, string publisher)
        {
            if (string.IsNullOrWhiteSpace(description)) return "";

            var s = WebUtility.HtmlDecode(description);
            s = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", " ");
            s = s.Replace(' ', ' ');
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s{2,}", " ").Trim();
            if (s.Length == 0) return "";

            // Başlık yankısını at.
            var h = headline.Trim();
            if (h.Length > 0 && s.StartsWith(h, StringComparison.OrdinalIgnoreCase))
                s = s[h.Length..].Trim(' ', '-', '–', '|', '·', ',');

            // Yayıncı adı kuyruğunu at.
            if (!string.IsNullOrWhiteSpace(publisher) && s.EndsWith(publisher, StringComparison.OrdinalIgnoreCase))
                s = s[..^publisher.Length].Trim(' ', '-', '–', '|', '·', ',');

            // Geriye anlamlı bir metin kalmadıysa gerçek içerik yoktur.
            return s.Length < 80 ? "" : s;
        }
    }
}
