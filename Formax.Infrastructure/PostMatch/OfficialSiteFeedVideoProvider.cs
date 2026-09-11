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
    /// RESMÎ SİTE AKIŞLARINDAN KEŞİF — federasyon / lig / yayıncı kendi yayımladığı
    /// RSS-Atom akışından okunur.
    ///
    /// NEDEN AKIŞ, NEDEN SAYFA KAZIMA DEĞİL: akış, sitenin KENDİ yayımladığı makine
    /// okunur uçtur; okumak için izin gerekmez, sayfa yapısına bağımlı değildir ve
    /// istek sayısı akış sayısı kadardır. Sayfa kazıma, tarayıcı sürme ve arama motoru
    /// döngüsü bu sınıfta YOKTUR ve eklenmeyecektir.
    ///
    /// YAPILANDIRILMAMIŞSA SESSİZ: <c>PostMatch:Video:OfficialFeeds</c> boşsa durum
    /// <see cref="VideoProviderStatuses.NotConfigured"/>'dır ve zincir yapılandırılmış
    /// sağlayıcılarla devam eder. Bir federasyonun akışını "bulmak" için tahmin
    /// üretilmez — adres verilmemişse o kaynak bu kurulumda yoktur.
    ///
    /// KAYNAK YİNE İZİN LİSTESİNDEN GEÇER: buradan çıkan aday da
    /// <see cref="MatchVideoIdentityValidator"/> kapısına girer; akışta görünmek
    /// "resmî" saymak için yeterli DEĞİLDİR — <c>SourceKey</c> izin listesindeki bir
    /// kayda karşılık gelmek zorundadır.
    ///
    /// ÖRNEK YAPILANDIRMA (appsettings):
    /// <code>
    /// "PostMatch": { "Video": { "OfficialFeeds": [
    ///   { "SourceKey": "uefa.com", "Url": "https://www.uefa.com/rssfeed/video/rss.xml" }
    /// ] } }
    /// </code>
    /// </summary>
    public sealed class OfficialSiteFeedVideoProvider : IOfficialMatchVideoProvider
    {
        private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
        private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";

        /// <summary>Aynı akış aynı turda birden çok maç için tekrar tekrar çekilmez.</summary>
        private static readonly TimeSpan FeedCache = TimeSpan.FromMinutes(20);

        private readonly IHttpClientFactory _httpFactory;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialSiteFeedVideoProvider> _log;

        public OfficialSiteFeedVideoProvider(
            IHttpClientFactory httpFactory, IMemoryCache cache,
            IConfiguration config, ILogger<OfficialSiteFeedVideoProvider> log)
        {
            _httpFactory = httpFactory; _cache = cache; _config = config; _log = log;
        }

        public string Name => "OfficialSiteFeeds";

        /// <summary>Hak sahibi kaynaklar — zincirin EN BAŞI.</summary>
        public int Priority => OfficialVideoSourceTiers.Federation;

        /// <summary>Yapılandırılmış akış tanımı.</summary>
        public sealed record FeedConfig(string SourceKey, string Url);

        /// <summary>
        /// Yapılandırılmış akışlar. Kaynak anahtarı izin listesinde YOKSA kayıt
        /// atlanır: adresi yapılandırmaya yazmak, kaynağı resmî yapmaz.
        /// </summary>
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
            foreach (var feed in feeds)
            {
                ct.ThrowIfCancellationRequested();
                all.AddRange(await GetFeedAsync(feed, ct).ConfigureAwait(false));
            }

            // Kaba zaman süzgeci — kimlik doğrulaması yine validator'da yapılır.
            var end = MatchVideoIdentityValidator.EndOf(fixture.MatchDateUtc);
            return all
                .Where(c => c.PublishedUtc >= end
                         && c.PublishedUtc <= end + MatchVideoIdentityValidator.PublishTail)
                .ToList();
        }

        private async Task<IReadOnlyList<OfficialVideoCandidate>> GetFeedAsync(
            FeedConfig feed, CancellationToken ct)
        {
            var cacheKey = "postmatch:sitefeed:" + feed.SourceKey;
            if (_cache.TryGetValue<IReadOnlyList<OfficialVideoCandidate>>(cacheKey, out var cached)
                && cached != null)
                return cached;

            try
            {
                var client = _httpFactory.CreateClient("postmatch-video");
                using var res = await client.GetAsync(feed.Url, ct).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                {
                    _log.LogWarning("[POST-MATCH VIDEO] resmi site akisi {Status}: {Key}",
                        (int)res.StatusCode, feed.SourceKey);
                    return Cache(cacheKey, Array.Empty<OfficialVideoCandidate>());
                }

                var xml = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return Cache(cacheKey, Parse(xml, feed.SourceKey, Name));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[POST-MATCH VIDEO] resmi site akisi okunamadi: {Key}", feed.SourceKey);
                return Cache(cacheKey, Array.Empty<OfficialVideoCandidate>());
            }
        }

        /// <summary>
        /// RSS 2.0 ve Atom akışlarını adaylara çevirir. Eksik alan uydurulmaz; kayıt atlanır.
        /// </summary>
        public static IReadOnlyList<OfficialVideoCandidate> Parse(
            string xml, string sourceKey, string providerName)
        {
            var list = new List<OfficialVideoCandidate>();
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch (System.Xml.XmlException) { return list; }

            // RSS 2.0
            foreach (var item in doc.Descendants("item"))
            {
                var link = (string?)item.Element("link");
                var title = (string?)item.Element("title");
                var pubRaw = (string?)item.Element("pubDate");
                if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(title)) continue;
                if (!TryParseDate(pubRaw, out var published)) continue;

                list.Add(Build(sourceKey, providerName, link!, title!,
                    (string?)item.Element("description"), published,
                    (string?)item.Element(Media + "thumbnail")?.Attribute("url")));
            }

            // Atom
            foreach (var entry in doc.Descendants(Atom + "entry"))
            {
                var link = entry.Elements(Atom + "link")
                                .Select(l => (string?)l.Attribute("href"))
                                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));
                var title = (string?)entry.Element(Atom + "title");
                var pubRaw = (string?)entry.Element(Atom + "published")
                             ?? (string?)entry.Element(Atom + "updated");
                if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(title)) continue;
                if (!TryParseDate(pubRaw, out var published)) continue;

                list.Add(Build(sourceKey, providerName, link!, title!,
                    (string?)entry.Element(Atom + "summary"), published,
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
                // Sayfa adresi kaynağın kendi kalıcı kimliğidir; yeniden yüklemede değişmez.
                ExternalVideoId: link,
                Title: title,
                Description: description,
                PublishedUtc: published,
                SourcePageUrl: link,
                ThumbnailUrl: thumb,
                DurationSeconds: null,
                ProviderName: providerName);

        private static bool TryParseDate(string? raw, out DateTime utc)
            => DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);

        private IReadOnlyList<OfficialVideoCandidate> Cache(
            string key, IReadOnlyList<OfficialVideoCandidate> value)
        {
            _cache.Set(key, value, FeedCache);
            return value;
        }
    }
}
