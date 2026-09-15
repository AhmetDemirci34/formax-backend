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
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>
    /// RESMÎ KANAL AKIŞINDAN VİDEO KEŞFİ.
    ///
    /// NEDEN RSS, NEDEN ARAMA MOTORU DEĞİL:
    ///  • Kanal akışı (<c>feeds/videos.xml</c>) YouTube'un YAYIMLADIĞI resmî uçtur;
    ///    anahtar istemez, kota yakmaz, sayfa kazımaz.
    ///  • En önemlisi: akış KANAL KİMLİĞİNE bağlıdır. Yani sonuç kümesi tanımı gereği
    ///    yalnız izin listesindeki resmî kanalların videolarıdır — arama motoru gibi
    ///    "başlığı uyan her yükleme" değil. Korsan yükleme buraya HİÇ giremez.
    ///  • Arama motorunu sınırsız döngüyle sorgulamak yasaktır; burada istek sayısı
    ///    kanal sayısı kadardır ve akışlar kısa süreli önbelleğe alınır.
    ///
    /// SINIR: akış kanalın YALNIZ SON ~15 videosunu verir. Bu bir kusur değil, akışın
    /// tasarımıdır ve toplama takvimini belirler: maç biter bitmez (60-90 dk) bakılır.
    /// Günler sonra ilk kez bakılan bir maçın özeti akıştan düşmüş olabilir; o zaman
    /// dürüst cevap "bulunamadı"dır, uydurma bir kayıt değil.
    /// </summary>
    public sealed class YouTubeChannelFeedVideoProvider : IOfficialMatchVideoProvider
    {
        private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
        private static readonly XNamespace Yt = "http://www.youtube.com/xml/schemas/2015";
        private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";

        /// <summary>Aynı kanal aynı turda birden çok maç için tekrar tekrar çekilmez.</summary>
        private static readonly TimeSpan FeedCache = TimeSpan.FromMinutes(20);

        private readonly IHttpClientFactory _httpFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<YouTubeChannelFeedVideoProvider> _log;
        private readonly Telemetry.VideoDiscoveryRequestLog? _requests;

        public YouTubeChannelFeedVideoProvider(
            IHttpClientFactory httpFactory, IMemoryCache cache, ILogger<YouTubeChannelFeedVideoProvider> log,
            Telemetry.VideoDiscoveryRequestLog? requests = null, IOfficialVideoSourceCatalog? catalog = null)
        {
            _httpFactory = httpFactory; _cache = cache; _log = log; _requests = requests; _catalog = catalog;
        }

        private readonly IOfficialVideoSourceCatalog? _catalog;

        public string Name => "YouTubeOfficialChannels";

        /// <summary>
        /// YARDIMCI KEŞİF — zincirin EN SONU. Kendi başına bir hak sahibi değildir;
        /// resmî kanalları okumanın anahtarsız yoludur ve son ~15 videoyla sınırlıdır.
        /// </summary>
        public int Priority => OfficialVideoSourceTiers.AuxiliaryDiscovery;

        /// <summary>
        /// KAPALI (15.09.2026, kullanıcı kararı): youtube.com/robots.txt <c>Disallow: /feeds/videos.xml</c>. "Feed okuyucu
        /// istisnası" kullanılmaz; bu sağlayıcı ağa hiç çıkmaz. Ayrıştırıcı (Parse) yalnız eski kayıtların testleri için durur.
        /// </summary>
        public string Status => VideoProviderStatuses.Disabled;

        public Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(
            VideoFixtureIdentity fixture, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OfficialVideoCandidate>>(Array.Empty<OfficialVideoCandidate>());

        /// <summary>Atom akışını adaylara çevirir. Eksik alan uydurulmaz; kayıt atlanır.</summary>
        public static IReadOnlyList<OfficialVideoCandidate> Parse(string xml, string channelId)
        {
            var doc = XDocument.Parse(xml);
            var list = new List<OfficialVideoCandidate>();

            foreach (var e in doc.Descendants(Atom + "entry"))
            {
                var videoId = (string?)e.Element(Yt + "videoId");
                var title = (string?)e.Element(Atom + "title");
                var publishedRaw = (string?)e.Element(Atom + "published");
                if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(title)) continue;
                if (!DateTime.TryParse(publishedRaw, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var published))
                    continue;

                var group = e.Element(Media + "group");
                var description = (string?)group?.Element(Media + "description");
                var thumbnail = (string?)group?.Element(Media + "thumbnail")?.Attribute("url");

                list.Add(new OfficialVideoCandidate(
                    Platform: "YouTube",
                    SourceIdentifier: channelId,
                    ExternalVideoId: videoId!,
                    Title: title!,
                    Description: description,
                    PublishedUtc: published,
                    SourcePageUrl: $"https://www.youtube.com/watch?v={videoId}",
                    ThumbnailUrl: thumbnail,
                    // Akış süre vermez. Uydurmak yerine null bırakılır; ekran süreyi
                    // yalnız gerçekten bilindiğinde gösterir.
                    DurationSeconds: null,
                    ProviderName: "YouTubeOfficialChannels"));
            }

            return list;
        }

    }

    /// <summary>
    /// KEŞİF KAPALI — sıfır dış istek.
    ///
    /// Operasyon video toplamayı kapatmak istediğinde devreye girer. Boş liste döner;
    /// akış geri kalanı (defter, tekrar takvimi, dürüst boş durum) aynen çalışır.
    /// </summary>
    public sealed class DisabledOfficialMatchVideoProvider : IOfficialMatchVideoProvider
    {
        public string Name => "Disabled";

        public int Priority => OfficialVideoSourceTiers.AuxiliaryDiscovery;

        public string Status => VideoProviderStatuses.Disabled;

        public Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(
            VideoFixtureIdentity fixture, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OfficialVideoCandidate>>(Array.Empty<OfficialVideoCandidate>());
    }
}
