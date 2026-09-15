using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>
    /// EMBED İZNİNİ KAYNAĞIN KENDİ RESMÎ UCUNDAN DOĞRULAR.
    ///
    /// YouTube için kullanılan uç <c>/oembed</c>'dir: gömme için YAYIMLANMIŞ resmî
    /// arayüzdür, anahtar istemez, kota yakmaz ve sayfayı scrape etmez. Video sahibi
    /// gömmeyi kapattığında bu uç 401/403 döner — cevabı "oynatılamaz" olarak alırız.
    ///
    /// AŞMA YOK: 401 alındığında farklı bir yol denenmez, proxy kurulmaz, referer
    /// taklit edilmez. Engel, yayıncının kararıdır.
    ///
    /// Kaynak politikası zaten "gömmeye kapalı" diyorsa (ör. uefa.com CSP) HİÇ istek
    /// atılmaz: bilinen bir hayırı tekrar tekrar sormak gereksiz dış istektir.
    /// </summary>
    public sealed class YouTubeEmbedVerifier : IVideoEmbedVerifier
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<YouTubeEmbedVerifier> _log;
        private readonly Telemetry.VideoDiscoveryRequestLog? _requests;

        public YouTubeEmbedVerifier(IHttpClientFactory httpFactory, ILogger<YouTubeEmbedVerifier> log,
            Telemetry.VideoDiscoveryRequestLog? requests = null)
        {
            _httpFactory = httpFactory; _log = log; _requests = requests;
        }

        public async Task<EmbedVerification> VerifyAsync(
            OfficialVideoCandidate candidate, OfficialVideoSource source, CancellationToken ct = default)
        {
            if (!source.AllowsInAppEmbed && !string.Equals(candidate.Platform, "YouTube", StringComparison.OrdinalIgnoreCase))
                return new EmbedVerification(false, null, null, source.Why);

            if (!string.Equals(candidate.Platform, "YouTube", StringComparison.OrdinalIgnoreCase))
                return new EmbedVerification(false, null, null, "gömme doğrulaması yalnız YouTube için tanımlı");

            var watchUrl = $"https://www.youtube.com/watch?v={candidate.ExternalVideoId}";
            var oembed = "https://www.youtube.com/oembed?format=json&url=" + Uri.EscapeDataString(watchUrl);

            try
            {
                var client = _httpFactory.CreateClient("postmatch-video");
                using var res = await client.GetAsync(oembed, ct).ConfigureAwait(false);
                _requests?.RecordRequest("YouTubeOEmbed", oembed, candidate.MatchId, candidate.ExternalFixtureId,
                    ((int)res.StatusCode).ToString(), 0);

                if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    return new EmbedVerification(false, null, null,
                        $"yayıncı gömmeyi kapatmış ya da video gizli (oembed {(int)res.StatusCode})", Unavailable: true);

                if (res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                    return new EmbedVerification(false, null, null,
                        $"video kaldırılmış/bulunamadı (oembed {(int)res.StatusCode})", Unavailable: true);

                if (!res.IsSuccessStatusCode)
                    return new EmbedVerification(false, null, null,
                        $"gömme doğrulanamadı (oembed {(int)res.StatusCode})");

                var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(body);
                var thumb = doc.RootElement.TryGetProperty("thumbnail_url", out var t) ? t.GetString() : null;
                var title = doc.RootElement.TryGetProperty("title", out var tt) ? tt.GetString() : null;
                var author = doc.RootElement.TryGetProperty("author_name", out var an) ? an.GetString() : null;
                var authorUrl = doc.RootElement.TryGetProperty("author_url", out var au) ? au.GetString() : null;

                // Yalnız çerezsiz gömme adresi kullanılır: kullanıcı izleme çerezi
                // toplamadan, FORMAX ekranının içinde oynatır.
                var embedUrl = $"https://www.youtube-nocookie.com/embed/{candidate.ExternalVideoId}";
                return new EmbedVerification(true, embedUrl, thumb, $"oembed 200 — gömmeye açık; kanal={author}", title, author, authorUrl);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Doğrulanamayan video OYNATILMAZ. "Herhalde açıktır" varsayımı, kullanıcıya
                // sonsuza dek dönen boş bir player göstermenin en kısa yoludur.
                _log.LogWarning(ex, "[POST-MATCH VIDEO] oembed dogrulamasi basarisiz: {Id}", candidate.ExternalVideoId);
                return new EmbedVerification(false, null, null, "gömme doğrulaması başarısız");
            }
        }
    }
}
