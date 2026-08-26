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
        private const int DefaultQueries = 3;
        private const int MaxQueriesHardCap = 5;
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

            var budget = query.QueryBudget > 0
                ? Math.Min(query.QueryBudget, MaxQueriesHardCap)
                : DefaultQueries;

            // Türk takımlarında tr-TR pazarı gerçek Türkçe yayıncıları döndürür.
            var market = query.Locale == "tr" ? "&mkt=tr-TR&setlang=tr" : "";

            foreach (var q in query.Queries.Take(budget))
            {
                ct.ThrowIfCancellationRequested();
                var url = $"https://www.bing.com/news/search?q={WebUtility.UrlEncode(q)}&format=rss{market}";
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

                // YAYINCI KAYBI DÜZELTİLDİ (14.08 ölçümü): Bing RSS'in <link>'i bir tıklama
                // yönlendirmesidir (bing.com/news/apiclick.aspx?...&url=<GERÇEK ADRES>). Host'unu
                // aldığımız için HER haberin yayıncısı "bing.com" olarak yazılıyordu; kaynak
                // kalitesi de arama motorunu puanlıyor, gerçek yayıncıyı değil → bilinmeyen (60)
                // → News:MinEvidenceSourceQuality (85) kapısında eleniyordu. Ölçüldü: gerçek
                // özet taşıyan 974 kanıdın 956'sı tam bu yüzden AI'a hiç ulaşmıyordu.
                // Gerçek adres zaten linkin İÇİNDE; yalnız çözülüp taşınıyor (yeni kaynak YOK).
                var realUrl = UnwrapRedirect(link);

                sink.Add(new NewsCandidate
                {
                    Headline = title,
                    // Bing GERÇEK snippet verir — korunur. Yalnız başlığın yankısı olan
                    // metinler ayıklanır (sahte özet taşınmaz).
                    Summary = RealSummary(item.Element("description")?.Value, title),
                    Url = realUrl,
                    PublishedUtc = pub,
                    Provider = Name,
                    Publisher = Host(realUrl),
                    Language = query.Locale == "tr" ? "tr" : "en",
                    League = query.League,
                    Country = query.Country,
                    FormaxMatchId = query.FormaxMatchId
                });
                taken++;
            }
        }

        private static string Host(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : "";

        /// <summary>
        /// Bing tıklama yönlendirmesinin içindeki gerçek yayıncı adresini çözer.
        /// Yönlendirme değilse adres olduğu gibi döner.
        /// </summary>
        private static string UnwrapRedirect(string link)
        {
            if (!Uri.TryCreate(link, UriKind.Absolute, out var u)) return link;
            if (!u.Host.Contains("bing.com", StringComparison.OrdinalIgnoreCase)) return link;

            foreach (var pair in u.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                if (eq <= 0 || !pair[..eq].Equals("url", StringComparison.OrdinalIgnoreCase)) continue;

                var target = WebUtility.UrlDecode(pair[(eq + 1)..]);
                if (!string.IsNullOrWhiteSpace(target) && Uri.TryCreate(target, UriKind.Absolute, out _))
                    return target;
            }
            return link;
        }

        private static string StripTags(string html) =>
            System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ").Trim();

        /// <summary>
        /// Bing snippet'i genelde GERÇEK içeriktir; olduğu gibi taşınır. Yalnız başlığın
        /// tekrarı olan ya da anlamlı uzunluğa ulaşmayan metinler "özet" sayılmaz —
        /// uydurma özet üretilmez, alan boş bırakılır.
        /// </summary>
        private static string RealSummary(string? description, string headline)
        {
            if (string.IsNullOrWhiteSpace(description)) return "";

            var s = WebUtility.HtmlDecode(StripTags(description)).Replace(' ', ' ');
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s{2,}", " ").Trim();
            if (s.Length == 0) return "";

            var h = (headline ?? "").Trim();
            if (h.Length > 0 && s.StartsWith(h, StringComparison.OrdinalIgnoreCase))
                s = s[h.Length..].Trim(' ', '-', '–', '|', '·', ',');

            return s.Length < 80 ? "" : s;
        }
    }
}
