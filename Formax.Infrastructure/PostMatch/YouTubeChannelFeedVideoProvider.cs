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
            Telemetry.VideoDiscoveryRequestLog? requests = null)
        {
            _httpFactory = httpFactory; _cache = cache; _log = log; _requests = requests;
        }

        /// <summary>Tek kanal akışının sonucu — başarısız okuma "boş akış" ile karışmasın.</summary>
        private sealed record FeedRead(IReadOnlyList<OfficialVideoCandidate> Entries, bool Ok, bool RateLimited);

        public string Name => "YouTubeOfficialChannels";

        /// <summary>
        /// YARDIMCI KEŞİF — zincirin EN SONU. Kendi başına bir hak sahibi değildir;
        /// resmî kanalları okumanın anahtarsız yoludur ve son ~15 videoyla sınırlıdır.
        /// </summary>
        public int Priority => OfficialVideoSourceTiers.AuxiliaryDiscovery;

        /// <summary>Anahtar istemez — her zaman çalışmaya hazırdır.</summary>
        public string Status => VideoProviderStatuses.Configured;

        public async Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(
            VideoFixtureIdentity fixture, CancellationToken ct = default)
        {
            var all = new List<OfficialVideoCandidate>();
            int ok = 0, failed = 0;
            var rateLimited = false;

            foreach (var source in OfficialVideoSources.DiscoverableYouTubeChannels(
                         fixture.HomeTeamName, fixture.AwayTeamName, fixture.LeagueId))
            {
                ct.ThrowIfCancellationRequested();
                var read = await GetChannelFeedAsync(source.YouTubeChannelId!, fixture, ct).ConfigureAwait(false);
                if (read.Ok) ok++; else failed++;
                rateLimited |= read.RateLimited;
                all.AddRange(read.Entries);
            }

            // HİÇBİR kanal okunamadıysa bu bir "bulunamadı" değil, ENGELdir: tur deneme sayılmaz.
            if (ok == 0 && failed > 0)
                throw new VideoProviderUnavailableException(Name, $"{failed} kanal akisinin hicbiri okunamadi", rateLimited);

            // Kaba zaman süzgeci — kimlik doğrulaması yine validator'da yapılır. Buradaki
            // amaç yalnız açıkça alakasız kayıtları taşımamaktır.
            var end = MatchVideoIdentityValidator.EndOf(fixture.MatchDateUtc);
            return all
                .Where(c => c.PublishedUtc >= end && c.PublishedUtc <= end + MatchVideoIdentityValidator.PublishTail)
                .ToList();
        }

        private async Task<FeedRead> GetChannelFeedAsync(
            string channelId, VideoFixtureIdentity fixture, CancellationToken ct)
        {
            var url = "https://www.youtube.com/feeds/videos.xml?channel_id=" + Uri.EscapeDataString(channelId);

            // Önbellekte YALNIZ başarılı okumalar durur: başarısızlığı 20 dk saklamak, geçici
            // bir hatayı "akış boş" diye kalıcılaştırıyordu.
            if (_cache.TryGetValue<IReadOnlyList<OfficialVideoCandidate>>(CacheKey(channelId), out var cached)
                && cached != null)
            {
                _requests?.RecordRequest(Name, url, fixture.MatchId, fixture.ExternalFixtureId, "cache", cached.Count);
                return new FeedRead(cached, true, false);
            }

            try
            {
                var client = _httpFactory.CreateClient("postmatch-video");
                using var res = await client.GetAsync(url, ct).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                {
                    _log.LogWarning("[POST-MATCH VIDEO] kanal akisi {Status}: {Channel}", (int)res.StatusCode, channelId);
                    _requests?.RecordRequest(Name, url, fixture.MatchId, fixture.ExternalFixtureId, ((int)res.StatusCode).ToString(), 0);
                    return new FeedRead(Array.Empty<OfficialVideoCandidate>(), false, (int)res.StatusCode == 429);
                }

                var xml = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var parsed = Parse(xml, channelId);
                _requests?.RecordRequest(Name, url, fixture.MatchId, fixture.ExternalFixtureId, ((int)res.StatusCode).ToString(), parsed.Count);
                return new FeedRead(Cache(channelId, parsed), true, false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[POST-MATCH VIDEO] kanal akisi okunamadi: {Channel}", channelId);
                _requests?.RecordRequest(Name, url, fixture.MatchId, fixture.ExternalFixtureId, ex.GetType().Name, 0);
                return new FeedRead(Array.Empty<OfficialVideoCandidate>(), false, false);
            }
        }

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

        private IReadOnlyList<OfficialVideoCandidate> Cache(string channelId, IReadOnlyList<OfficialVideoCandidate> value)
        {
            _cache.Set(CacheKey(channelId), value, FeedCache);
            return value;
        }

        private static string CacheKey(string channelId) => "postmatch:video:feed:" + channelId;
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
