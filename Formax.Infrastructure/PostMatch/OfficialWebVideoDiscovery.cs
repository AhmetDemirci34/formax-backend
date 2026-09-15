using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>
    /// TUR BAŞINA DIŞ İSTEK BÜTÇESİ — keşif turu (scope) boyunca sayfa/akış/oEmbed isteklerinin üst sınırı.
    /// Bütçe bitince tur yeni maç almaz; kalan maçlar planlı olarak sonraki turda aranır (kaybolmaz).
    /// </summary>
    public sealed class VideoHttpBudget
    {
        private int _remaining;
        public VideoHttpBudget(IConfiguration config) => _remaining = Math.Max(1, config.GetValue("PostMatch:Video:MaxHttpPerCycle", 150));
        public int Remaining => Volatile.Read(ref _remaining);
        public int Used { get; private set; }
        public bool TryConsume()
        {
            if (Interlocked.Decrement(ref _remaining) < 0) { Interlocked.Increment(ref _remaining); return false; }
            Used++;
            return true;
        }
    }

    /// <summary>Resmî sayfadaki JSON-LD <c>VideoObject</c>.</summary>
    public sealed record WebVideoObject(string? Name, DateTime? UploadUtc, bool UploadExact, string? YouTubeId, string? Url);

    /// <summary>Resmî sayfa ayrıştırma sonucu — script ÇALIŞTIRILMAZ, yalnız metin okunur.</summary>
    public sealed record OfficialPageFacts(
        IReadOnlyList<WebVideoObject> Videos,
        IReadOnlyList<string> IframeYouTubeIds,
        IReadOnlyList<string> SameAs,
        IReadOnlyList<string> WikidataIds,
        IReadOnlyList<string> YouTubeHandles,
        IReadOnlyList<(string Href, string Type)> FeedLinks,
        IReadOnlyList<(string Href, string Text)> ExternalLinks,
        string? Title);

    /// <summary>
    /// RESMÎ SAYFA AYRIŞTIRICI (saf). Kaynak türleri: JSON-LD <c>VideoObject</c> (embedUrl/contentUrl), YouTube iframe
    /// gömmesi, <c>sameAs</c> (Wikidata QID + resmî YouTube handle), <c>link rel=alternate</c> RSS/Atom, dış bağlantılar.
    /// YouTube kimliği yalnız bu işaretlerden okunur; sayfadaki rastgele metin kimlik sayılmaz.
    /// </summary>
    public static class OfficialWebPageParser
    {
        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static readonly Regex JsonLdBlock = new(@"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>([\s\S]*?)</script>", Opts);
        private static readonly Regex YouTubeId = new(@"(?:youtube(?:-nocookie)?\.com/(?:embed/|watch\?v=|v/)|youtu\.be/)([A-Za-z0-9_-]{11})(?![A-Za-z0-9_-])", Opts);
        private static readonly Regex Iframe = new(@"<iframe\b[^>]*\bsrc\s*=\s*[""']([^""']+)[""']", Opts);
        private static readonly Regex LinkAlternate = new(@"<link\b[^>]*>", Opts);
        private static readonly Regex Anchor = new(@"<a\b[^>]*\bhref\s*=\s*[""'](https?://[^""'#\s]+)[""'][^>]*>([\s\S]{0,400}?)</a>", Opts);
        private static readonly Regex TitleTag = new(@"<title[^>]*>([\s\S]*?)</title>", Opts);
        private static readonly Regex WikidataQ = new(@"wikidata\.org/(?:wiki|entity)/(Q[0-9]+)", Opts);
        private static readonly Regex YouTubeHandle = new(@"youtube\.com/(?:(@[A-Za-z0-9._-]{2,64})|channel/(UC[A-Za-z0-9_-]{22})|c/([A-Za-z0-9._-]{2,64})|user/([A-Za-z0-9._-]{2,64}))", Opts);
        private static readonly Regex Tags = new(@"<[^>]+>", Opts);
        private static readonly Regex AltAttr = new(@"\b(?:alt|title|aria-label)\s*=\s*[""']([^""']{2,120})[""']", Opts);

        public static string? ExtractYouTubeId(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var m = YouTubeId.Match(url);
            return m.Success ? m.Groups[1].Value : null;
        }

        public static OfficialPageFacts Parse(string html, string pageUrl)
        {
            html ??= string.Empty;
            var videos = new List<WebVideoObject>();
            var sameAs = new List<string>();
            foreach (System.Text.RegularExpressions.Match block in JsonLdBlock.Matches(html))
            {
                var raw = WebUtilityDecodeJson(block.Groups[1].Value);
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    Walk(doc.RootElement, videos, sameAs, 0);
                    continue;
                }
                catch (JsonException) { }
                try
                {
                    // Sık görülen bozukluk: dize içinde ham satır sonu/sekme. Denetim karakterleri boşluğa çevrilip yeniden denenir.
                    using var doc = JsonDocument.Parse(Regex.Replace(raw, @"[\x00-\x1F]", " "));
                    Walk(doc.RootElement, videos, sameAs, 0);
                    continue;
                }
                catch (JsonException) { }
                // Son çare: bozuk blokta VideoObject nesnesi başına alan okuma (yalnız embedUrl/contentUrl'de YouTube kimliği varsa).
                foreach (System.Text.RegularExpressions.Match vo in Regex.Matches(raw, @"""@type""\s*:\s*""VideoObject""", Opts))
                {
                    var start = raw.LastIndexOf('{', vo.Index);
                    var end = raw.IndexOf('}', vo.Index);
                    if (start < 0 || end < 0) continue;
                    var obj = raw[start..(end + 1)];
                    string? F(string name) => Regex.Match(obj, @"""" + name + @"""\s*:\s*""((?:[^""\\]|\\.)*)""", Opts) is { Success: true } m ? Regex.Unescape(m.Groups[1].Value) : null;
                    var id = ExtractYouTubeId(F("embedUrl")) ?? ExtractYouTubeId(F("contentUrl"));
                    if (id == null) continue;
                    var (upload, exact) = ParseDate(F("uploadDate"));
                    videos.Add(new WebVideoObject(System.Net.WebUtility.HtmlDecode(F("name") ?? string.Empty).Trim(), upload, exact, id, F("url")));
                }
            }

            var iframeIds = Iframe.Matches(html).Select(m => ExtractYouTubeId(m.Groups[1].Value))
                .Where(x => x != null).Select(x => x!).Distinct(StringComparer.Ordinal).ToList();

            var feeds = new List<(string, string)>();
            foreach (System.Text.RegularExpressions.Match l in LinkAlternate.Matches(html))
            {
                var tag = l.Value;
                if (!Regex.IsMatch(tag, @"rel\s*=\s*[""']alternate[""']", Opts)) continue;
                var type = Regex.Match(tag, @"type\s*=\s*[""'](application/(?:rss|atom)\+xml)[""']", Opts);
                var href = Regex.Match(tag, @"href\s*=\s*[""']([^""']+)[""']", Opts);
                if (!type.Success || !href.Success) continue;
                var abs = Absolute(pageUrl, System.Net.WebUtility.HtmlDecode(href.Groups[1].Value));
                if (abs == null || abs.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)) continue;
                if (Regex.IsMatch(abs, @"comment", Opts)) continue;
                feeds.Add((abs, type.Groups[1].Value.ToLowerInvariant().Contains("atom") ? "Atom" : "Rss"));
            }

            var pageHost = Uri.TryCreate(pageUrl, UriKind.Absolute, out var pu) ? pu.Host : string.Empty;
            var external = new List<(string, string)>();
            foreach (System.Text.RegularExpressions.Match a in Anchor.Matches(html))
            {
                if (!Uri.TryCreate(a.Groups[1].Value, UriKind.Absolute, out var u)) continue;
                if (SameSite(u.Host, pageHost)) continue;
                var inner = a.Groups[2].Value;
                var text = System.Net.WebUtility.HtmlDecode(Tags.Replace(inner, " ")).Trim();
                var alt = AltAttr.Match(inner);
                var full = Regex.Replace((text + " " + (alt.Success ? alt.Groups[1].Value : "") + " " + Regex.Match(a.Value, @"\b(?:title|aria-label)\s*=\s*[""']([^""']+)", Opts).Groups[1].Value), @"\s+", " ").Trim();
                // YouTube bağlantısında kimlik sorgu dizesindedir (?v=); kesilmez.
                var keepQuery = u.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase);
                external.Add((keepQuery ? u.AbsoluteUri : u.GetLeftPart(UriPartial.Path), full));
            }

            var allLinks = sameAs.Concat(Regex.Matches(html, @"href\s*=\s*[""'](https?://(?:www\.)?youtube\.com/[^""']+)", Opts).Select(m => m.Groups[1].Value));
            var handles = allLinks.Select(x => YouTubeHandle.Match(x)).Where(m => m.Success)
                .Select(m => (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? "c/" + m.Groups[3].Value : "user/" + m.Groups[4].Value).ToLowerInvariant())
                .Distinct(StringComparer.Ordinal).Take(8).ToList();

            var qids = sameAs.Select(x => WikidataQ.Match(x)).Where(m => m.Success).Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.Ordinal).ToList();

            var title = TitleTag.Match(html) is { Success: true } t ? System.Net.WebUtility.HtmlDecode(t.Groups[1].Value).Trim() : null;
            return new OfficialPageFacts(videos, iframeIds, sameAs, qids, handles, feeds.Distinct().ToList(), external, title);
        }

        /// <summary>
        /// Sayfanın ANA videosu: JSON-LD'de tek bir YouTube videosu varsa o; birden çoksa başlığı eşleşen tek video; JSON-LD
        /// yoksa tek iframe. Birden çok aday ve eşleşme yoksa null (Ambiguous) — hangisinin maç videosu olduğu bilinemez.
        /// </summary>
        public static (WebVideoObject? Video, string? IframeId, string Outcome) PrimaryVideo(OfficialPageFacts facts, string? expectedTitle)
        {
            var yt = facts.Videos.Where(v => v.YouTubeId != null).ToList();
            var distinct = yt.Select(v => v.YouTubeId).Distinct(StringComparer.Ordinal).ToList();
            if (distinct.Count == 1) return (yt[0], null, "Ok");
            if (distinct.Count > 1)
            {
                var folded = MatchVideoIdentityValidator.Fold(expectedTitle);
                var byName = yt.Where(v => folded.Length > 0 && string.Equals(MatchVideoIdentityValidator.Fold(v.Name), folded, StringComparison.Ordinal))
                    .Select(v => v.YouTubeId).Distinct(StringComparer.Ordinal).ToList();
                return byName.Count == 1 ? (yt.First(v => v.YouTubeId == byName[0]), null, "Ok") : (null, null, "Ambiguous");
            }
            if (facts.IframeYouTubeIds.Count == 1) return (null, facts.IframeYouTubeIds[0], "Ok");
            return (null, null, facts.IframeYouTubeIds.Count > 1 ? "Ambiguous" : "NoVideo");
        }

        private static void Walk(JsonElement e, List<WebVideoObject> videos, List<string> sameAs, int depth)
        {
            if (depth > 12) return;
            if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in e.EnumerateArray()) Walk(item, videos, sameAs, depth + 1);
                return;
            }
            if (e.ValueKind != JsonValueKind.Object) return;

            if (e.TryGetProperty("sameAs", out var sa))
            {
                if (sa.ValueKind == JsonValueKind.String) sameAs.Add(sa.GetString()!);
                else if (sa.ValueKind == JsonValueKind.Array)
                    foreach (var x in sa.EnumerateArray()) if (x.ValueKind == JsonValueKind.String) sameAs.Add(x.GetString()!);
            }

            var type = e.TryGetProperty("@type", out var t) ? (t.ValueKind == JsonValueKind.String ? t.GetString() : t.ToString()) : null;
            if (type != null && type.Contains("VideoObject", StringComparison.Ordinal))
            {
                string? S(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                var id = ExtractYouTubeId(S("embedUrl")) ?? ExtractYouTubeId(S("contentUrl")) ?? ExtractYouTubeId(S("url"));
                var (upload, exact) = ParseDate(S("uploadDate") ?? S("datePublished"));
                videos.Add(new WebVideoObject(System.Net.WebUtility.HtmlDecode(S("name") ?? string.Empty).Trim(), upload, exact, id, S("url")));
            }

            foreach (var p in e.EnumerateObject())
                if (p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) Walk(p.Value, videos, sameAs, depth + 1);
        }

        /// <summary>Tarih + hassasiyet: "2026-09-12" → (gün, false); "2026-09-12T19:00:00Z" → (an, true).</summary>
        public static (DateTime? Utc, bool Exact) ParseDate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return (null, false);
            var s = raw.Trim();
            if (Regex.IsMatch(s, @"^\d{4}-\d{2}-\d{2}$"))
                return DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
                    ? (DateTime.SpecifyKind(d, DateTimeKind.Utc), false) : (null, false);
            return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)
                ? (dto.UtcDateTime, true) : (null, false);
        }

        public static bool SameSite(string a, string b)
        {
            static string Root(string h)
            {
                var parts = h.ToLowerInvariant().Split('.');
                if (parts.Length <= 2) return string.Join('.', parts);
                // co.uk / com.tr gibi iki parçalı ülke uzantıları
                var sld = parts[^2];
                return sld is "co" or "com" or "org" or "net" or "gov" or "ac" && parts.Length >= 3
                    ? string.Join('.', parts[^3..]) : string.Join('.', parts[^2..]);
            }
            return !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && Root(a) == Root(b);
        }

        public static string? Absolute(string baseUrl, string href)
            => Uri.TryCreate(baseUrl, UriKind.Absolute, out var b) && Uri.TryCreate(b, href, out var u)
               && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) ? u.AbsoluteUri : null;

        private static string WebUtilityDecodeJson(string s) => s.Trim().Replace("&quot;", "\"", StringComparison.Ordinal);

        public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        // ── AKIŞ AYRIŞTIRMA ─────────────────────────────────────────────────────────

        private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";
        private static readonly XNamespace VideoNs = "http://www.google.com/schemas/sitemap-video/1.1";
        private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

        public sealed record FeedItem(string PageUrl, string Title, DateTime? PublishedUtc, bool Exact, string? YouTubeId);

        /// <summary>Video sitemap / sitemap index / RSS 2.0 / Atom. Dönüş: öğeler + (index ise) alt sitemap adresleri.</summary>
        public static (IReadOnlyList<FeedItem> Items, IReadOnlyList<(string Loc, DateTime? LastMod)> Children) ParseFeed(string xml)
        {
            var items = new List<FeedItem>();
            var children = new List<(string, DateTime?)>();
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch (System.Xml.XmlException) { return (items, children); }

            foreach (var sm in doc.Descendants(SitemapNs + "sitemap"))
            {
                var loc = ((string?)sm.Element(SitemapNs + "loc"))?.Trim();
                if (string.IsNullOrWhiteSpace(loc)) continue;
                children.Add((loc!, ParseDate((string?)sm.Element(SitemapNs + "lastmod")).Utc));
            }

            foreach (var url in doc.Descendants(SitemapNs + "url"))
            {
                var loc = ((string?)url.Element(SitemapNs + "loc"))?.Trim();
                // Juventus sitemap'i <video:videos> yazıyor (ölçüldü) — iki biçim de okunur.
                var video = url.Element(VideoNs + "video") ?? url.Element(VideoNs + "videos");
                if (loc == null || video == null) continue;
                var title = ((string?)video.Element(VideoNs + "title"))?.Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;
                var (pub, exact) = ParseDate((string?)video.Element(VideoNs + "publication_date"));
                var id = ExtractYouTubeId((string?)video.Element(VideoNs + "player_loc")) ?? ExtractYouTubeId((string?)video.Element(VideoNs + "content_loc"));
                items.Add(new FeedItem(loc, System.Net.WebUtility.HtmlDecode(title!), pub, exact, id));
            }

            foreach (var item in doc.Descendants("item"))
            {
                var link = ((string?)item.Element("link"))?.Trim();
                var title = ((string?)item.Element("title"))?.Trim();
                if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(title)) continue;
                DateTime? pub = null; var exact = false;
                if (DateTime.TryParse((string?)item.Element("pubDate"), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var p)) { pub = DateTime.SpecifyKind(p, DateTimeKind.Utc); exact = true; }
                items.Add(new FeedItem(link!, System.Net.WebUtility.HtmlDecode(title!), pub, exact, null));
            }

            foreach (var entry in doc.Descendants(Atom + "entry"))
            {
                var link = entry.Elements(Atom + "link").Select(l => (string?)l.Attribute("href")).FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));
                var title = ((string?)entry.Element(Atom + "title"))?.Trim();
                if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(title)) continue;
                var (pub, exact) = ParseDate((string?)entry.Element(Atom + "published") ?? (string?)entry.Element(Atom + "updated"));
                items.Add(new FeedItem(link!, System.Net.WebUtility.HtmlDecode(title!), pub, exact, null));
            }
            return (items, children);
        }

        /// <summary>Başlık bir maç videosu olabilir mi? (depolama süzgeci — kimlik kararı DEĞİL)</summary>
        public static bool LooksLikeMatchVideo(string title)
        {
            var f = MatchVideoIdentityValidator.Fold(title);
            return MatchVideoIdentityValidator.HasHighlightMarker(f)
                   || MatchVideoIdentityValidator.ClassifyType(f) != null
                   || MatchVideoIdentityValidator.CountsDistinctFixtures(title) == 1
                   || Regex.IsMatch(f, @"\b\d{1,2}\s*[-–:]\s*\d{1,2}\b");
        }
    }

    /// <summary>
    /// RESMÎ SİTE AKIŞ TARAYICISI — doğrulanmış resmî sitelerin (katalog, WebsiteStatus=Verified) makine okunur akışlarını
    /// otomatik bulur ve kalıcı <see cref="OfficialWebVideoEntry"/> girişlerine yazar. Bütün istekler nezaket katmanından
    /// geçer (robots.txt RFC 9309, host aralığı, devre kesici). Kaynak başına hata sayısı ve devre durumu katalogda kalıcıdır.
    /// </summary>
    public sealed class OfficialWebFeedCrawler
    {
        public const string ProviderName = "OfficialWebFeeds";
        private const int MaxEntriesPerFeed = 3000;
        public const long MaxBodyBytes = 12_000_000;

        private readonly FormaxDbContext _db;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<OfficialWebFeedCrawler> _log;
        private readonly Telemetry.VideoDiscoveryRequestLog? _requests;

        public OfficialWebFeedCrawler(FormaxDbContext db, IHttpClientFactory http, ILogger<OfficialWebFeedCrawler> log,
            Telemetry.VideoDiscoveryRequestLog? requests = null)
        {
            _db = db; _http = http; _log = log; _requests = requests;
        }

        public int HttpCalls { get; private set; }

        public sealed record FetchResult(int? Status, string Body, string? Error);

        public async Task<FetchResult> GetAsync(string url, int? matchId, CancellationToken ct)
        {
            HttpCalls++;
            try
            {
                var client = _http.CreateClient(OfficialVideoSourceDiscoveryService.HttpClientName);
                using var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                var code = (int)res.StatusCode;
                if (!res.IsSuccessStatusCode)
                {
                    _requests?.RecordRequest(ProviderName, url, matchId, null, code.ToString(CultureInfo.InvariantCulture), 0);
                    return new FetchResult(code, string.Empty, res.ReasonPhrase);
                }
                // Dev düz URL sitemap'leri (ölçüldü: assets.laliga.com 38 MB, <video:video> etiketi yok) her turda indirilmez.
                if (res.Content.Headers.ContentLength is long len && len > MaxBodyBytes)
                {
                    _requests?.RecordRequest(ProviderName, url, matchId, null, "TooLarge", 0);
                    return new FetchResult(413, string.Empty, $"gövde çok büyük ({len} bayt)");
                }
                var bytes = await res.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                if (url.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var gz = new System.IO.Compression.GZipStream(new System.IO.MemoryStream(bytes), System.IO.Compression.CompressionMode.Decompress);
                        using var ms = new System.IO.MemoryStream();
                        await gz.CopyToAsync(ms, ct).ConfigureAwait(false);
                        bytes = ms.ToArray();
                    }
                    catch (System.IO.InvalidDataException) { }
                }
                var body = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 8_000_000));
                _requests?.RecordRequest(ProviderName, url, matchId, null, code.ToString(CultureInfo.InvariantCulture), 0);
                return new FetchResult(code, body, null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _requests?.RecordRequest(ProviderName, url, matchId, null, ex.GetType().Name, 0);
                return new FetchResult(null, string.Empty, ex.GetType().Name);
            }
        }

        /// <summary>
        /// Doğrulanmış bir resmî sitenin akışlarını bulur: robots.txt <c>Sitemap:</c> satırları → (index ise) video adlı alt
        /// sitemap'ler; ana sayfadaki YouTube dışı RSS/Atom; ana sayfa JSON-LD VideoObject varsa ana sayfanın kendisi.
        /// </summary>
        public async Task<int> DiscoverFeedsAsync(OfficialVideoSourceRecord source, DateTime nowUtc, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(source.Domain)) return 0;
            var origin = "https://" + source.Domain;
            var found = new List<(string Url, string Kind, string Via)>();

            var robots = await GetAsync(origin + "/robots.txt", null, ct).ConfigureAwait(false);
            var state = RobotsTxtPolicy.StateFor(robots.Status);
            source.RobotsStatus = RobotsTxtPolicy.Summarize(state == RobotsTxtPolicy.RobotsState.Parsed ? RobotsTxtPolicy.ParseRules(robots.Body) : new List<RobotsRule>(), state);
            if (state == RobotsTxtPolicy.RobotsState.Unreachable)
            {
                RecordFailure(source, nowUtc, "robots.txt ulaşılamadı: " + (robots.Status?.ToString(CultureInfo.InvariantCulture) ?? robots.Error));
                return 0;
            }

            var sitemaps = state == RobotsTxtPolicy.RobotsState.Parsed
                ? Regex.Matches(robots.Body, @"^\s*sitemap:\s*(\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline).Select(m => m.Groups[1].Value.Trim()).Distinct().Take(12).ToList()
                : new List<string>();
            foreach (var sm in sitemaps)
            {
                ct.ThrowIfCancellationRequested();
                if (!Uri.TryCreate(sm, UriKind.Absolute, out var smUri) || !OfficialWebPageParser.SameSite(smUri.Host, source.Domain!)) continue;
                var isVideo = Regex.IsMatch(sm, @"video|media|highlight", RegexOptions.IgnoreCase);
                if (isVideo) { found.Add((sm, "VideoSitemap", "robots:Sitemap")); continue; }
                var index = await GetAsync(sm, null, ct).ConfigureAwait(false);
                if (index.Status is not (>= 200 and < 300) || !index.Body.Contains("<sitemapindex", StringComparison.OrdinalIgnoreCase)) continue;
                var (_, children) = OfficialWebPageParser.ParseFeed(index.Body);
                foreach (var (loc, _) in children.Where(c => Regex.IsMatch(c.Loc, @"video|highlight", RegexOptions.IgnoreCase))
                             .OrderByDescending(c => c.LastMod ?? DateTime.MinValue).Take(24))
                    found.Add((loc, "VideoSitemap", "sitemap-index"));
            }

            var home = await GetAsync(origin + "/", null, ct).ConfigureAwait(false);
            if (home.Status is >= 200 and < 300)
            {
                var facts = OfficialWebPageParser.Parse(home.Body, origin + "/");
                foreach (var (href, kind) in facts.FeedLinks.Take(4))
                    if (Uri.TryCreate(href, UriKind.Absolute, out var fu) && OfficialWebPageParser.SameSite(fu.Host, source.Domain!))
                        found.Add((href, kind, "html:link-alternate"));
                if (facts.Videos.Any(v => v.YouTubeId != null) || facts.IframeYouTubeIds.Count > 0)
                    found.Add((origin + "/", "HomePage", "homepage"));
                if (facts.YouTubeHandles.Count > 0) source.SiteYouTubeHandles = Trim(string.Join(",", facts.YouTubeHandles), 600);
            }

            var added = 0;
            foreach (var (url, kind, via) in found.DistinctBy(f => f.Url))
            {
                if (url.Length > 450) continue;
                if (await _db.OfficialWebFeeds.AnyAsync(f => f.Url == url, ct).ConfigureAwait(false)) continue;
                _db.OfficialWebFeeds.Add(new OfficialWebFeed
                {
                    SourceKey = source.Key, Url = url, Kind = kind, DiscoveredVia = via, IsActive = true,
                    NextFetchUtc = nowUtc, CreatedAtUtc = nowUtc
                });
                added++;
            }
            source.FeedsDiscoveredAtUtc = nowUtc;
            source.LastSuccessUtc = nowUtc;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return added;
        }

        /// <summary>Zamanı gelen akışları okur ve girişleri kalıcı yazar. Dönüş: (okunan akış, yeni giriş).</summary>
        public async Task<(int Feeds, int NewEntries)> CrawlDueAsync(DateTime nowUtc, int maxFeeds, CancellationToken ct)
        {
            var due = await _db.OfficialWebFeeds.Where(f => f.IsActive && f.NextFetchUtc <= nowUtc)
                .OrderBy(f => f.NextFetchUtc).Take(maxFeeds).ToListAsync(ct).ConfigureAwait(false);
            var sources = await _db.OfficialVideoSourceCatalog.ToDictionaryAsync(r => r.Key, StringComparer.OrdinalIgnoreCase, ct).ConfigureAwait(false);
            int feeds = 0, fresh = 0;
            foreach (var feed in due)
            {
                ct.ThrowIfCancellationRequested();
                sources.TryGetValue(feed.SourceKey, out var source);
                if (source != null && (!source.IsActive || source.CircuitOpenUntilUtc > nowUtc))
                {
                    feed.NextFetchUtc = source.CircuitOpenUntilUtc ?? nowUtc.AddHours(1);
                    continue;
                }
                feeds++;
                var res = await GetAsync(feed.Url, null, ct).ConfigureAwait(false);
                feed.LastFetchedUtc = nowUtc;
                feed.LastHttpStatus = res.Status;
                if (res.Status == 451)
                {
                    // robots.txt bu yolu yasaklıyor: adaptör kapanır, diğer akışlar sürer. Engel AŞILMAZ.
                    feed.IsActive = false; feed.RobotsStatus = "Disallowed"; feed.LastError = "robots.txt disallow";
                    continue;
                }
                if (res.Status == 413)
                {
                    feed.IsActive = false; feed.LastError = res.Error; feed.LastHttpStatus = 413;
                    continue;
                }
                if (res.Status is not (>= 200 and < 300))
                {
                    feed.FailureCount++;
                    feed.LastError = Trim($"HTTP {res.Status?.ToString(CultureInfo.InvariantCulture) ?? res.Error}", 400);
                    feed.NextFetchUtc = nowUtc + TimeSpan.FromMinutes(30 * Math.Pow(2, Math.Min(feed.FailureCount, 6)));
                    if (source != null) RecordFailure(source, nowUtc, $"{feed.Url}: {feed.LastError}");
                    continue;
                }

                feed.FailureCount = 0; feed.LastError = null; feed.RobotsStatus = "Allowed";
                if (feed.Kind == "VideoSitemap" && !res.Body.Contains("<video:", StringComparison.OrdinalIgnoreCase)
                    && !res.Body.Contains("<sitemapindex", StringComparison.OrdinalIgnoreCase))
                {
                    // Adı "video" geçse de video girişi taşımayan düz sitemap: adaptör kapanır, boşuna yeniden indirilmez.
                    feed.IsActive = false; feed.LastError = "video:video etiketi yok (düz URL sitemap'i)";
                    continue;
                }
                if (source != null) { source.FailureCount = 0; source.LastSuccessUtc = nowUtc; source.CircuitState = "Closed"; source.CircuitOpenUntilUtc = null; }

                var items = new List<OfficialWebPageParser.FeedItem>();
                if (feed.Kind == "HomePage")
                {
                    var facts = OfficialWebPageParser.Parse(res.Body, feed.Url);
                    foreach (var v in facts.Videos.Where(v => v.YouTubeId != null && !string.IsNullOrWhiteSpace(v.Name)))
                        items.Add(new OfficialWebPageParser.FeedItem(
                            OfficialWebPageParser.Absolute(feed.Url, v.Url ?? "") ?? feed.Url + "#yt-" + v.YouTubeId, v.Name!, v.UploadUtc, v.UploadExact, v.YouTubeId));
                    // Ana sayfada metni olan doğrudan YouTube bağlantıları: tarih yok (yalnız görülme anı) → kimlik kapısı skor+yön ister.
                    foreach (var (href, text) in facts.ExternalLinks)
                    {
                        var id = OfficialWebPageParser.ExtractYouTubeId(href);
                        if (id == null || text.Length < 10 || items.Any(i => i.YouTubeId == id)) continue;
                        items.Add(new OfficialWebPageParser.FeedItem(feed.Url + "#yt-" + id, text, null, false, id));
                    }
                }
                else
                {
                    var (parsed, children) = OfficialWebPageParser.ParseFeed(res.Body);
                    items.AddRange(parsed);
                    foreach (var (loc, _) in children.Where(c => Regex.IsMatch(c.Loc, @"video|highlight", RegexOptions.IgnoreCase)).Take(24))
                        if (loc.Length <= 450 && !await _db.OfficialWebFeeds.AnyAsync(f => f.Url == loc, ct).ConfigureAwait(false))
                            _db.OfficialWebFeeds.Add(new OfficialWebFeed
                            {
                                SourceKey = feed.SourceKey, Url = loc, Kind = "VideoSitemap", DiscoveredVia = "sitemap-index",
                                IsActive = true, NextFetchUtc = nowUtc, CreatedAtUtc = nowUtc
                            });
                }
                if (feed.Kind is "Rss" or "Atom")
                    items = items.Where(i => OfficialWebPageParser.LooksLikeMatchVideo(i.Title)).ToList();

                fresh += await UpsertEntriesAsync(feed, items.Take(MaxEntriesPerFeed).ToList(), nowUtc, ct).ConfigureAwait(false);
                feed.LastEntryCount = items.Count;
                // Ay adlı arşiv sitemap'i (ör. …/videos/2026-07.xml) değişmez: haftada bir. Güncel akışlar 30 dk.
                var archive = Regex.Match(feed.Url, @"(20\d{2})-(\d{2})");
                var isOldArchive = archive.Success && new DateTime(int.Parse(archive.Groups[1].Value, CultureInfo.InvariantCulture),
                    Math.Clamp(int.Parse(archive.Groups[2].Value, CultureInfo.InvariantCulture), 1, 12), 1) < new DateTime(nowUtc.Year, nowUtc.Month, 1);
                feed.NextFetchUtc = nowUtc + (isOldArchive ? TimeSpan.FromDays(7) : TimeSpan.FromMinutes(30));
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return (feeds, fresh);
        }

        private async Task<int> UpsertEntriesAsync(OfficialWebFeed feed, List<OfficialWebPageParser.FeedItem> items, DateTime nowUtc, CancellationToken ct)
        {
            if (items.Count == 0) return 0;
            var hashes = items.Select(i => OfficialWebPageParser.Hash(i.PageUrl)).Distinct().ToList();
            var existing = new Dictionary<string, OfficialWebVideoEntry>(StringComparer.Ordinal);
            foreach (var chunk in hashes.Chunk(500))
            {
                var list = chunk.ToList();
                foreach (var e in await _db.OfficialWebVideoEntries.Where(x => list.Contains(x.PageUrlHash)).ToListAsync(ct).ConfigureAwait(false))
                    existing[e.PageUrlHash] = e;
            }
            var added = 0;
            foreach (var i in items)
            {
                var h = OfficialWebPageParser.Hash(i.PageUrl);
                if (existing.TryGetValue(h, out var row))
                {
                    row.LastSeenUtc = nowUtc;
                    if (row.YouTubeVideoId == null && i.YouTubeId != null) { row.YouTubeVideoId = i.YouTubeId; row.VideoIdEvidence = feed.Kind == "HomePage" ? "JsonLdEmbedUrl" : "Sitemap"; }
                    continue;
                }
                if (i.PageUrl.Length > 1000) continue;
                row = new OfficialWebVideoEntry
                {
                    SourceKey = feed.SourceKey, FeedId = feed.Id, PageUrl = i.PageUrl, PageUrlHash = h,
                    Title = Trim(i.Title, 400), FoldedTitle = Trim(MatchVideoIdentityValidator.Fold(i.Title), 400),
                    PublishedUtc = i.PublishedUtc,
                    DatePrecision = i.PublishedUtc == null ? MatchVideoIdentityValidator.PrecisionSeenOnly
                        : i.Exact ? MatchVideoIdentityValidator.PrecisionExact : MatchVideoIdentityValidator.PrecisionDay,
                    YouTubeVideoId = i.YouTubeId,
                    VideoIdEvidence = i.YouTubeId == null ? null : feed.Kind == "HomePage" ? "JsonLdEmbedUrl" : "Sitemap",
                    PageOutcome = i.YouTubeId == null ? null : "Ok",
                    FirstSeenUtc = nowUtc, LastSeenUtc = nowUtc
                };
                _db.OfficialWebVideoEntries.Add(row);
                existing[h] = row;
                added++;
            }
            return added;
        }

        /// <summary>Kaynak düzeyi hata: 5 ardışık hatada devre açılır (1, 2, 4 … en çok 24 saat).</summary>
        public static void RecordFailure(OfficialVideoSourceRecord source, DateTime nowUtc, string error)
        {
            source.FailureCount++;
            source.LastError = Trim(error, 400);
            if (source.FailureCount >= 5)
            {
                var hours = Math.Min(24, Math.Pow(2, Math.Min(source.FailureCount - 5, 5)));
                source.CircuitState = "Open";
                source.CircuitOpenUntilUtc = nowUtc.AddHours(hours);
            }
        }

        /// <summary>Girişin sayfasını okuyup ana YouTube videosunu çıkarır (yalnız gerektiğinde, bütçeyle).</summary>
        public async Task FetchPageAsync(OfficialWebVideoEntry entry, int? matchId, DateTime nowUtc, CancellationToken ct)
        {
            var res = await GetAsync(entry.PageUrl, matchId, ct).ConfigureAwait(false);
            entry.PageFetchedAtUtc = nowUtc;
            entry.PageHttpStatus = res.Status;
            if (res.Status == 451) { entry.PageOutcome = "RobotsDisallowed"; return; }
            if (res.Status is not (>= 200 and < 300)) { entry.PageOutcome = "Error"; return; }
            var facts = OfficialWebPageParser.Parse(res.Body, entry.PageUrl);
            var (video, iframeId, outcome) = OfficialWebPageParser.PrimaryVideo(facts, entry.Title);
            entry.PageOutcome = outcome;
            if (video != null)
            {
                entry.YouTubeVideoId = video.YouTubeId;
                entry.VideoIdEvidence = "JsonLdEmbedUrl";
                entry.JsonLdName = string.IsNullOrWhiteSpace(video.Name) ? null : Trim(video.Name!, 400);
                if (video.UploadUtc != null && video.UploadExact) entry.JsonLdUploadUtc = video.UploadUtc;
            }
            else if (iframeId != null)
            {
                entry.YouTubeVideoId = iframeId;
                entry.VideoIdEvidence = "Iframe";
            }
        }

        private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
    }

    /// <summary>
    /// RESMÎ WEB VİDEO SAĞLAYICISI — maçın iki kulübü, ligi ve ligin doğrulanmış yayıncılarının RESMÎ SİTELERİNDE
    /// yayımlanmış video girişlerinden aday üretir. YouTube RSS/Data API kullanılmaz; YouTube kimliği yalnız resmî sayfanın
    /// kendi JSON-LD/iframe gömmesinden okunur. Kimlik kararı <see cref="MatchVideoIdentityValidator"/>'dadır.
    /// </summary>
    public sealed class OfficialWebVideoProvider : IOfficialMatchVideoProvider
    {
        private readonly FormaxDbContext _db;
        private readonly OfficialWebFeedCrawler _crawler;
        private readonly VideoHttpBudget _budget;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialWebVideoProvider> _log;

        public OfficialWebVideoProvider(FormaxDbContext db, OfficialWebFeedCrawler crawler, VideoHttpBudget budget,
            IConfiguration config, ILogger<OfficialWebVideoProvider> log)
        {
            _db = db; _crawler = crawler; _budget = budget; _config = config; _log = log;
        }

        public string Name => "OfficialWebSites";
        public int Priority => OfficialVideoSourceTiers.Federation;
        public string Status => _config.GetValue("PostMatch:Video:OfficialWebEnabled", true) ? VideoProviderStatuses.Configured : VideoProviderStatuses.Disabled;

        /// <summary>Son turda okunan resmî sayfa sayısı (teşhis).</summary>
        public int LastPageFetches { get; private set; }

        public async Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(VideoFixtureIdentity fixture, CancellationToken ct = default)
        {
            LastPageFetches = 0;
            var nowUtc = DateTime.UtcNow;
            var sources = await RelevantSourcesAsync(fixture, ct).ConfigureAwait(false);
            if (sources.Count == 0) return Array.Empty<OfficialVideoCandidate>();

            var keys = sources.Select(s => s.Key).ToList();
            var end = MatchVideoIdentityValidator.EndOf(fixture.MatchDateUtc);
            var from = fixture.MatchDateUtc.Date.AddDays(-1);
            var to = end + MatchVideoIdentityValidator.PublishTail + TimeSpan.FromDays(1);
            var entries = await _db.OfficialWebVideoEntries
                .Where(e => keys.Contains(e.SourceKey)
                            && ((e.PublishedUtc != null && e.PublishedUtc >= from && e.PublishedUtc <= to)
                                || (e.PublishedUtc == null && e.FirstSeenUtc >= fixture.MatchDateUtc)))
                .ToListAsync(ct).ConfigureAwait(false);

            var relevant = entries
                .Select(e => new
                {
                    Entry = e,
                    Home = TeamNameAliases.Mentions(e.FoldedTitle, fixture.HomeTeamName),
                    Away = TeamNameAliases.Mentions(e.FoldedTitle, fixture.AwayTeamName)
                })
                .Where(x => x.Home || x.Away)
                .OrderByDescending(x => x.Home && x.Away)
                .ThenByDescending(x => MatchVideoIdentityValidator.HasHighlightMarker(x.Entry.FoldedTitle))
                .Take(Math.Max(1, _config.GetValue("PostMatch:Video:MaxEntriesPerMatch", 12)))
                .Select(x => x.Entry)
                .ToList();

            var maxPages = Math.Max(0, _config.GetValue("PostMatch:Video:MaxPageFetchesPerMatch", 6));
            var candidates = new List<OfficialVideoCandidate>();
            foreach (var e in relevant)
            {
                ct.ThrowIfCancellationRequested();
                var needsPage = e.YouTubeVideoId == null
                    && (e.PageOutcome == null || (e.PageOutcome == "Error" && e.PageFetchedAtUtc < nowUtc.AddHours(-12)));
                if (needsPage && LastPageFetches < maxPages && _budget.TryConsume())
                {
                    LastPageFetches++;
                    await _crawler.FetchPageAsync(e, fixture.MatchId, nowUtc, ct).ConfigureAwait(false);
                }
                if (e.YouTubeVideoId == null) continue;

                var exactUpload = e.JsonLdUploadUtc;
                var published = exactUpload ?? e.PublishedUtc ?? e.FirstSeenUtc;
                var precision = exactUpload != null ? MatchVideoIdentityValidator.PrecisionExact
                    : e.PublishedUtc == null ? MatchVideoIdentityValidator.PrecisionSeenOnly : e.DatePrecision;
                candidates.Add(new OfficialVideoCandidate(
                    Platform: "YouTube",
                    SourceIdentifier: e.SourceKey,
                    ExternalVideoId: e.YouTubeVideoId,
                    // Başlık resmî sayfanın kendi video adıdır (JSON-LD), yoksa akıştaki başlık.
                    Title: e.JsonLdName ?? e.Title,
                    Description: null,
                    PublishedUtc: published,
                    SourcePageUrl: e.PageUrl,
                    ThumbnailUrl: null,
                    DurationSeconds: null,
                    ProviderName: Name,
                    MatchId: fixture.MatchId,
                    ExternalFixtureId: fixture.ExternalFixtureId,
                    DatePrecision: precision,
                    EvidencePageUrl: e.PageUrl));
            }
            if (_db.ChangeTracker.HasChanges()) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return candidates;
        }

        /// <summary>Maçın kulüpleri + kapsamı bu ligi içeren lig/federasyon/yayıncı siteleri (site doğrulanmış, etkin, devre kapalı).</summary>
        public async Task<List<OfficialVideoSourceRecord>> RelevantSourcesAsync(VideoFixtureIdentity fixture, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var rows = await _db.OfficialVideoSourceCatalog.AsNoTracking()
                .Where(r => r.WebsiteStatus == OfficialVideoSourceCatalog.StatusVerified && r.Domain != null && r.IsActive
                            && (r.CircuitOpenUntilUtc == null || r.CircuitOpenUntilUtc < now))
                .ToListAsync(ct).ConfigureAwait(false);
            return rows.Where(r =>
                r.TeamId is int t ? t == fixture.HomeTeamId || t == fixture.AwayTeamId
                : string.IsNullOrWhiteSpace(r.LeagueIds) || fixture.LeagueId is not int l
                    || r.LeagueIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(l.ToString(CultureInfo.InvariantCulture)))
                .ToList();
        }
    }
}
