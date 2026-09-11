using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>
    /// RESMÎ KANALDA TARİH ARALIĞIYLA ARAMA — YouTube Data API (anahtar gerektirir).
    ///
    /// NEDEN VAR: RSS akışı kanalın yalnız son ~15 videosunu verir. Yayın yoğun bir
    /// hafta sonunda resmî özet, ilk bakıştan önce akıştan düşebiliyor. Data API aynı
    /// resmî kanalın içinde TARİH ARALIĞIYLA arama yapar; böylece keşif, akışın
    /// uzunluğuna değil maçın zamanına bağlanır.
    ///
    /// ANAHTAR YOKSA: <see cref="VideoProviderStatuses.NotConfigured"/>. Sağlayıcı hiç
    /// çağrılmaz, durumu kayda geçer ve zincir yapılandırılmış olanlarla devam eder.
    /// ANAHTAR ÜRETİLMEZ, başkasının anahtarı kullanılmaz, kotasız gizli uç aranmaz.
    ///
    /// KAPSAM DARDIR: arama YALNIZ izin listesindeki kanal kimlikleri içinde yapılır
    /// (channelId parametresi). Serbest arama YAPILMAZ; korsan yükleme sonuç kümesine
    /// tanımı gereği giremez.
    /// </summary>
    public sealed class YouTubeDataApiVideoProvider : IOfficialMatchVideoProvider
    {
        private const string SearchEndpoint = "https://www.googleapis.com/youtube/v3/search";

        /// <summary>Kanal başına en fazla sonuç — kota bir maça sınırsız harcanmaz.</summary>
        private const int MaxResultsPerChannel = 10;

        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<YouTubeDataApiVideoProvider> _log;
        private readonly Telemetry.VideoDiscoveryRequestLog? _requests;

        public YouTubeDataApiVideoProvider(
            IHttpClientFactory httpFactory, IConfiguration config, ILogger<YouTubeDataApiVideoProvider> log,
            Telemetry.VideoDiscoveryRequestLog? requests = null)
        {
            _httpFactory = httpFactory; _config = config; _log = log; _requests = requests;
        }

        public string Name => "YouTubeDataApi";

        /// <summary>
        /// Aynı resmî kanalları okur ama akış sınırından bağımsızdır; bu yüzden yardımcı
        /// keşfin (RSS) ÖNÜNDE, hak sahibi kaynakların ARKASINDA çalışır.
        /// </summary>
        public int Priority => OfficialVideoSourceTiers.LicensedSportsOutlet;

        private string? ApiKey => _config["PostMatch:Video:YouTubeDataApi:ApiKey"];

        public string Status =>
            !_config.GetValue("PostMatch:Video:YouTubeDataApi:Enabled", true)
                ? VideoProviderStatuses.Disabled
                : string.IsNullOrWhiteSpace(ApiKey)
                    ? VideoProviderStatuses.NotConfigured
                    : VideoProviderStatuses.Configured;

        public async Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(
            VideoFixtureIdentity fixture, CancellationToken ct = default)
        {
            var key = ApiKey;
            if (string.IsNullOrWhiteSpace(key)) return Array.Empty<OfficialVideoCandidate>();

            var end = MatchVideoIdentityValidator.EndOf(fixture.MatchDateUtc);
            var before = end + MatchVideoIdentityValidator.PublishTail;

            var results = new List<OfficialVideoCandidate>();
            var channels = OfficialVideoSources.DiscoverableYouTubeChannels(
                fixture.HomeTeamName, fixture.AwayTeamName);

            foreach (var source in channels)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    results.AddRange(
                        await SearchChannelAsync(source, key!, end, before, fixture, ct).ConfigureAwait(false));
                }
                catch (OperationCanceledException) { throw; }
                // Kota/plan engeli tüm kanalları etkiler: tur ENGELLENDİ olarak zincire bildirilir.
                catch (VideoProviderUnavailableException) { throw; }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "[POST-MATCH VIDEO] Data API aramasi basarisiz: {Channel}", source.Key);
                }
            }

            return results;
        }

        private async Task<IReadOnlyList<OfficialVideoCandidate>> SearchChannelAsync(
            OfficialVideoSource source, string apiKey, DateTime after, DateTime before,
            VideoFixtureIdentity fixture, CancellationToken ct)
        {
            var url = SearchEndpoint
                + "?part=snippet&type=video&order=date"
                + "&maxResults=" + MaxResultsPerChannel
                + "&channelId=" + Uri.EscapeDataString(source.YouTubeChannelId!)
                + "&publishedAfter=" + Uri.EscapeDataString(Iso(after))
                + "&publishedBefore=" + Uri.EscapeDataString(Iso(before))
                + "&key=" + Uri.EscapeDataString(apiKey);

            var client = _httpFactory.CreateClient("postmatch-video");
            using var res = await client.GetAsync(url, ct).ConfigureAwait(false);
            var code = (int)res.StatusCode;
            if (!res.IsSuccessStatusCode)
            {
                // Kayda YALNIZ host + yol düşer; anahtar sorgudadır ve yazılmaz.
                _requests?.RecordRequest(Name, url, fixture.MatchId, fixture.ExternalFixtureId, code.ToString(), 0);
                _log.LogWarning("[POST-MATCH VIDEO] Data API {Status}: {Channel}", code, source.Key);
                if (code is 403 or 429)
                    throw new VideoProviderUnavailableException(Name, $"Data API {code} (kota/plan)", rateLimited: true);
                return Array.Empty<OfficialVideoCandidate>();
            }

            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var parsed = Parse(json, source.YouTubeChannelId!, Name);
            _requests?.RecordRequest(Name, url, fixture.MatchId, fixture.ExternalFixtureId, code.ToString(), parsed.Count);
            return parsed;
        }

        /// <summary>Data API yanıtını adaylara çevirir. Eksik alan uydurulmaz; kayıt atlanır.</summary>
        public static IReadOnlyList<OfficialVideoCandidate> Parse(
            string json, string channelId, string providerName)
        {
            var list = new List<OfficialVideoCandidate>();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items)) return list;

            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var id)
                    || !id.TryGetProperty("videoId", out var vid)) continue;
                if (!item.TryGetProperty("snippet", out var sn)) continue;

                var videoId = vid.GetString();
                var title = sn.TryGetProperty("title", out var t) ? t.GetString() : null;
                var publishedRaw = sn.TryGetProperty("publishedAt", out var p) ? p.GetString() : null;
                if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(title)) continue;
                if (!DateTime.TryParse(publishedRaw, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var published))
                    continue;

                // KANAL KİMLİĞİ SONUÇTAN OKUNUR, istekten değil: yanıt beklenmedik bir
                // kanaldan geldiyse aday izin listesi kapısında düşsün.
                var actualChannel = sn.TryGetProperty("channelId", out var cid)
                    ? cid.GetString() : channelId;

                string? thumb = null;
                if (sn.TryGetProperty("thumbnails", out var th)
                    && th.TryGetProperty("high", out var hi)
                    && hi.TryGetProperty("url", out var hu))
                    thumb = hu.GetString();

                list.Add(new OfficialVideoCandidate(
                    Platform: "YouTube",
                    SourceIdentifier: actualChannel ?? channelId,
                    ExternalVideoId: videoId!,
                    Title: title!,
                    Description: sn.TryGetProperty("description", out var d) ? d.GetString() : null,
                    PublishedUtc: published,
                    SourcePageUrl: "https://www.youtube.com/watch?v=" + videoId,
                    ThumbnailUrl: thumb,
                    DurationSeconds: null,
                    ProviderName: providerName));
            }

            return list;
        }

        private static string Iso(DateTime utc)
            => utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }
}
