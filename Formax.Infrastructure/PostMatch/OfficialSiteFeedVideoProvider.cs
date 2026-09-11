using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>
    /// RESMÎ SİTE AKIŞLARINDAN KEŞİF — federasyon / lig / yayıncı / kulübün KENDİ
    /// yayımladığı makine okunur akıştan okunur: RSS 2.0, Atom ya da Google video sitemap.
    ///
    /// NEDEN AKIŞ, NEDEN SAYFA KAZIMA DEĞİL: akış, sitenin arama motorlarına ve okuyuculara
    /// KENDİSİNİN sunduğu uçtur; sayfa yapısına bağımlı değildir ve istek sayısı akış sayısı
    /// kadardır. Sayfa kazıma, tarayıcı sürme, bot korumasını (ör. Cloudflare) aşma ve arama
    /// motoru döngüsü bu sınıfta YOKTUR.
    ///
    /// ÖLÇÜLDÜ (11.09.2026, robots.txt → sitemap zinciri):
    ///  • trtspor.com.tr/sitemap_video.xml — 200, 200 video girişi, maç özetleri dahil.
    ///  • fenerbahce.org — Cloudflare bot sınaması; AŞILMAZ, yapılandırılmaz.
    ///  • uefa.com — bağlantı kurulamadı; yapılandırılmaz.
    ///  • beinsports.com.tr/server-sitemap.xml — 500; yapılandırılmaz.
    ///
    /// YAYIN ZAMANI TUZAĞI (aynı ölçüm): TRT SPOR sitemap'indeki <c>publication_date</c>
    /// maçtan SAATLER önce (ör. Real Madrid–Inter: 12:30Z, maç 19:00Z) — sayfa maç
    /// öncesinde açılıyor. Bu alan video yayın anı DEĞİLDİR ve kimlik kapısı bu yüzden
    /// adayı REDDEDER. Bu sınıf tarihi "düzeltmez": ön süzgeç adayı yalnız kaba pencereyle
    /// taşır, kararı ve gerekçesini doğrulayıcı verir.
    ///
    /// YAPILANDIRILMAMIŞSA SESSİZ: <c>PostMatch:Video:OfficialFeeds</c> boşsa durum
    /// <see cref="VideoProviderStatuses.NotConfigured"/>'dır ve zincir devam eder.
    /// Kaynak anahtarı izin listesinde (<see cref="OfficialVideoSources"/>) yoksa akış
    /// ATLANIR — adresi yapılandırmaya yazmak kaynağı resmî yapmaz.
    /// </summary>
    public sealed class OfficialSiteFeedVideoProvider : IOfficialMatchVideoProvider
    {
        private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
        private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";
        private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";
        private static readonly XNamespace VideoNs = "http://www.google.com/schemas/sitemap-video/1.1";

        /// <summary>Aynı akış aynı turda birden çok maç için tekrar tekrar çekilmez.</summary>
        private static readonly TimeSpan FeedCache = TimeSpan.FromMinutes(20);

        /// <summary>
        /// Ön süzgecin kickoff'tan GERİYE bakışı. Kimlik kararı burada verilmez; bu pencere
        /// yalnız açıkça ilgisiz (günler önceki) girişleri taşımamak içindir.
        /// </summary>
        public static readonly TimeSpan PreKickoffWindow = TimeSpan.FromHours(24);

        private readonly IHttpClientFactory _httpFactory;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialSiteFeedVideoProvider> _log;
        private readonly Telemetry.VideoDiscoveryRequestLog? _requests;

        public OfficialSiteFeedVideoProvider(
            IHttpClientFactory httpFactory, IMemoryCache cache,
            IConfiguration config, ILogger<OfficialSiteFeedVideoProvider> log,
            Telemetry.VideoDiscoveryRequestLog? requests = null)
        {
            _httpFactory = httpFactory; _cache = cache; _config = config; _log = log; _requests = requests;
        }

        public string Name => "OfficialSiteFeeds";

        /// <summary>Hak sahibi siteler — zincirin EN BAŞI.</summary>
        public int Priority => OfficialVideoSourceTiers.Federation;

        /// <summary>Yapılandırılmış akış tanımı.</summary>
        public sealed record FeedConfig(string SourceKey, string Url);

        /// <summary>Yapılandırılmış ve izin listesinde karşılığı olan akışlar.</summary>
        public IReadOnlyList<FeedConfig> Feeds
        {
            get
            {
                var list = new List<FeedConfig>();
                foreach (var section in _config.GetSection("PostMatch:Video:OfficialFeeds").GetChildren())
                {
                    var key = section["SourceKey"];
                    var url = section["Url"];
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(url)) continue;
                    if (OfficialVideoSources.ByKey(key) == null)
                    {
                        _log.LogWarning(
                            "[POST-MATCH VIDEO] akis kaynagi izin listesinde degil, atlandi: {Key}", key);
                        continue;
                    }
                    list.Add(new FeedConfig(key!, url!));
                }
                return list;
            }
        }

        public string Status =>
            !_config.GetValue("PostMatch:Video:OfficialFeedsEnabled", true)
                ? VideoProviderStatuses.Disabled
                : Feeds.Count == 0
                    ? VideoProviderStatuses.NotConfigured
                    : VideoProviderStatuses.Configured;

        public async Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(
            VideoFixtureIdentity fixture, CancellationToken ct = default)
        {
            var feeds = Feeds;
            if (feeds.Count == 0) return Array.Empty<OfficialVideoCandidate>();

            var all = new List<OfficialVideoCandidate>();
            int ok = 0, failed = 0;
            var rateLimited = false;

            foreach (var feed in feeds)
            {
                ct.ThrowIfCancellationRequested();
                var read = await GetFeedAsync(feed, fixture, ct).ConfigureAwait(false);
                if (read.Ok) ok++; else failed++;
                rateLimited |= read.RateLimited;
                all.AddRange(read.Entries);
            }

            if (ok == 0 && failed > 0)
                throw new VideoProviderUnavailableException(Name, $"{failed} resmi site akisinin hicbiri okunamadi", rateLimited);

            return Prefilter(all, fixture);
        }

        /// <summary>
        /// KABA PENCERE — kickoff'tan 24 saat önce ile yayın kuyruğunun sonu arası.
        /// Kimlik (yön, ayak, yayın anı, tür) doğrulayıcıdadır; burası yalnız açıkça ilgisiz
        /// girişleri taşımamak içindir. Pencere bilerek GENİŞ: maç öncesi tarihlenmiş bir
        /// sayfanın ret gerekçesi ("maç bitmeden yayımlanmış") görünür kalsın.
        /// </summary>
        public static IReadOnlyList<OfficialVideoCandidate> Prefilter(
            IEnumerable<OfficialVideoCandidate> entries, VideoFixtureIdentity fixture)
        {
            var from = fixture.MatchDateUtc - PreKickoffWindow;
            var to = MatchVideoIdentityValidator.EndOf(fixture.MatchDateUtc) + MatchVideoIdentityValidator.PublishTail;
            return entries.Where(c => c.PublishedUtc >= from && c.PublishedUtc <= to).ToList();
        }

        private sealed record FeedRead(IReadOnlyList<OfficialVideoCandidate> Entries, bool Ok, bool RateLimited);

        private async Task<FeedRead> GetFeedAsync(FeedConfig feed, VideoFixtureIdentity fixture, CancellationToken ct)
        {
            var cacheKey = "postmatch:sitefeed:" + feed.SourceKey;
            if (_cache.TryGetValue<IReadOnlyList<OfficialVideoCandidate>>(cacheKey, out var cached) && cached != null)
            {
                _requests?.RecordRequest(Name, feed.Url, fixture.MatchId, fixture.ExternalFixtureId, "cache", cached.Count);
                return new FeedRead(cached, true, false);
            }

            try
            {
                var client = _httpFactory.CreateClient("postmatch-video");
                using var res = await client.GetAsync(feed.Url, ct).ConfigureAwait(false);
                var code = (int)res.StatusCode;
                if (!res.IsSuccessStatusCode)
                {
                    _log.LogWarning("[POST-MATCH VIDEO] resmi site akisi {Status}: {Key}", code, feed.SourceKey);
                    _requests?.RecordRequest(Name, feed.Url, fixture.MatchId, fixture.ExternalFixtureId, code.ToString(), 0);
                    return new FeedRead(Array.Empty<OfficialVideoCandidate>(), false, code == 429);
                }

                var xml = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var parsed = Parse(xml, feed.SourceKey, Name);
                _requests?.RecordRequest(Name, feed.Url, fixture.MatchId, fixture.ExternalFixtureId, code.ToString(), parsed.Count);

                // Yalnız başarılı okuma önbelleğe girer.
                _cache.Set(cacheKey, parsed, FeedCache);
                return new FeedRead(parsed, true, false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[POST-MATCH VIDEO] resmi site akisi okunamadi: {Key}", feed.SourceKey);
                _requests?.RecordRequest(Name, feed.Url, fixture.MatchId, fixture.ExternalFixtureId, ex.GetType().Name, 0);
                return new FeedRead(Array.Empty<OfficialVideoCandidate>(), false, false);
            }
        }

        /// <summary>
        /// RSS 2.0, Atom ve Google video sitemap girişlerini adaylara çevirir.
        /// Eksik alan UYDURULMAZ; başlığı, adresi ya da tarihi olmayan giriş atlanır.
        /// </summary>
        public static IReadOnlyList<OfficialVideoCandidate> Parse(
            string xml, string sourceKey, string providerName)
        {
            var list = new List<OfficialVideoCandidate>();
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch (System.Xml.XmlException) { return list; }

            // Google video sitemap — <url><loc/><video:video>…</video:video></url>
            foreach (var url in doc.Descendants(SitemapNs + "url"))
            {
                var video = url.Element(VideoNs + "video");
                if (video == null) continue;

                var loc = Clean((string?)url.Element(SitemapNs + "loc"));
                var title = Clean((string?)video.Element(VideoNs + "title"));
                var pubRaw = (string?)video.Element(VideoNs + "publication_date");
                if (loc == null || title == null || !TryParseDate(pubRaw, out var published)) continue;

                list.Add(Build(sourceKey, providerName, loc, title,
                    Clean((string?)video.Element(VideoNs + "description")), published,
                    Clean((string?)video.Element(VideoNs + "thumbnail_loc"))));
            }

            // RSS 2.0
            foreach (var item in doc.Descendants("item"))
            {
                var link = Clean((string?)item.Element("link"));
                var title = Clean((string?)item.Element("title"));
                if (link == null || title == null || !TryParseDate((string?)item.Element("pubDate"), out var published))
                    continue;

                list.Add(Build(sourceKey, providerName, link, title,
                    Clean((string?)item.Element("description")), published,
                    (string?)item.Element(Media + "thumbnail")?.Attribute("url")));
            }

            // Atom
            foreach (var entry in doc.Descendants(Atom + "entry"))
            {
                var link = entry.Elements(Atom + "link")
                                .Select(l => (string?)l.Attribute("href"))
                                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));
                var title = Clean((string?)entry.Element(Atom + "title"));
                var pubRaw = (string?)entry.Element(Atom + "published") ?? (string?)entry.Element(Atom + "updated");
                if (string.IsNullOrWhiteSpace(link) || title == null || !TryParseDate(pubRaw, out var published))
                    continue;

                list.Add(Build(sourceKey, providerName, link!, title,
                    Clean((string?)entry.Element(Atom + "summary")), published,
                    (string?)entry.Element(Media + "thumbnail")?.Attribute("url")));
            }

            return list;
        }

        private static OfficialVideoCandidate Build(
            string sourceKey, string providerName, string link, string title,
            string? description, DateTime published, string? thumb)
            => new(
                Platform: "Web",
                // Web kaynaklarında KİMLİK, izin listesindeki anahtardır — sayfa adresi değil.
                SourceIdentifier: sourceKey,
                // Sayfa adresi kaynağın kendi kalıcı kimliğidir.
                ExternalVideoId: link,
                Title: title,
                Description: description,
                PublishedUtc: published,
                SourcePageUrl: link,
                ThumbnailUrl: thumb,
                DurationSeconds: null,
                ProviderName: providerName);

        /// <summary>CDATA etrafındaki boşlukları atar; boşsa null.</summary>
        private static string? Clean(string? raw)
        {
            var s = raw?.Trim();
            return string.IsNullOrEmpty(s) ? null : s;
        }

        private static bool TryParseDate(string? raw, out DateTime utc)
            => DateTime.TryParse(raw?.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
    }
}
