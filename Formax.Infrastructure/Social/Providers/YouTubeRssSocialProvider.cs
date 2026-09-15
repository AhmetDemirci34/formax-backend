using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Formax.Application.Services.Social.Discovery;
using Formax.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Social.Providers
{
    /// <summary>
    /// Phase 7 — YouTube resmi kanal Atom/RSS sağlayıcısı. GERÇEK, ücretsiz, key GEREKTİRMEZ:
    /// https://www.youtube.com/feeds/videos.xml?user={handle} veya ?channel_id={UC...}.
    /// Yalnız registry'deki doğrulanmış resmi hesabı okur. Hata/erişimsizlik → boş (fake yok).
    /// </summary>
    public sealed class YouTubeRssSocialProvider : ISocialProvider
    {
        private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
        private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";
        private const int MaxItems = 10;

        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<YouTubeRssSocialProvider> _logger;

        public YouTubeRssSocialProvider(IHttpClientFactory httpFactory, ILogger<YouTubeRssSocialProvider> logger)
        {
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public string Platform => "YouTube";

        /// <summary>
        /// KAPALI (15.09.2026, kullanıcı kararı): youtube.com/robots.txt <c>Disallow: /feeds/videos.xml</c>.
        /// "Feed okuyucu istisnası" kullanılmaz; bu yola hiçbir otomatik istek gönderilmez. Sağlayıcı DI'da
        /// kalır ama ağa çıkmaz — YouTube hesapları bu kanaldan boş döner.
        /// </summary>
        public bool IsEnabled => false;

        public bool CanHandle(OfficialSocialAccount account)
            => string.Equals(account.Platform, "YouTube", StringComparison.OrdinalIgnoreCase);

        public async Task<IReadOnlyList<SocialCandidate>> FetchAsync(
            OfficialSocialAccount account, CancellationToken ct = default)
        {
            if (!IsEnabled) return Array.Empty<SocialCandidate>();

            var url = ResolveFeedUrl(account);
            if (string.IsNullOrWhiteSpace(url)) return Array.Empty<SocialCandidate>();

            try
            {
                using var client = _httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(20);

                // USER-AGENT ZORUNLU: UA'sız istekte YouTube bazı kanalların feed'ine
                // 404 döndürüyor. Ölçüldü (20.08.2026) — HNK Hajduk Split resmi kanalı
                // (UCN7oOG6iLGDXXhyxBKcbq7A): UA'sız HTTP 404, UA ile HTTP 200 / 15 video.
                // Bu yüzden bazı doğrulanmış resmi hesaplar sessizce boş dönüyordu.
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) FormaxBot/1.0");

                var xml = await client.GetStringAsync(url, ct);
                var doc = XDocument.Parse(xml);

                var channelName = doc.Root?.Element(Atom + "author")?.Element(Atom + "name")?.Value
                                  ?? doc.Root?.Element(Atom + "title")?.Value
                                  ?? account.AccountName;

                var items = new List<SocialCandidate>();
                foreach (var e in doc.Descendants(Atom + "entry").Take(MaxItems))
                {
                    var title = e.Element(Atom + "title")?.Value ?? "";
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    var link = e.Elements(Atom + "link")
                                .FirstOrDefault(l => (string?)l.Attribute("rel") == "alternate")
                               ?? e.Element(Atom + "link");
                    var href = (string?)link?.Attribute("href") ?? "";

                    var publishedRaw = e.Element(Atom + "published")?.Value;
                    DateTime.TryParse(publishedRaw, null,
                        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                        out var published);

                    var desc = e.Element(Media + "group")?.Element(Media + "description")?.Value ?? "";
                    var summary = desc.Length > 400 ? desc[..400] : desc;

                    items.Add(new SocialCandidate
                    {
                        Platform = "YouTube",
                        AccountHandle = account.Handle,
                        AccountName = channelName,
                        Title = title.Trim(),
                        Summary = summary.Trim(),
                        Url = href,
                        PublishedUtc = published == default ? DateTime.UtcNow : published
                    });
                }

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SOCIAL] YouTube feed fetch failed for {Handle}", account.Handle);
                return Array.Empty<SocialCandidate>();
            }
        }

        private static string ResolveFeedUrl(OfficialSocialAccount account)
        {
            if (!string.IsNullOrWhiteSpace(account.FeedUrl)) return account.FeedUrl;
            if (string.IsNullOrWhiteSpace(account.Handle)) return "";
            var h = account.Handle.Trim();
            // "UC..." = channel_id, aksi halde legacy username.
            return h.StartsWith("UC", StringComparison.Ordinal) && h.Length >= 20
                ? $"https://www.youtube.com/feeds/videos.xml?channel_id={Uri.EscapeDataString(h)}"
                : $"https://www.youtube.com/feeds/videos.xml?user={Uri.EscapeDataString(h)}";
        }
    }
}
