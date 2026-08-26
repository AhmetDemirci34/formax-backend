using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Http
{
    /// <summary>
    /// api-football HTTP dayanıklılık katmanı (MVP Release Hardening).
    ///
    /// YENİ PAKET YOK — yalnız BCL + mevcut Microsoft.Extensions.Http (DelegatingHandler).
    ///
    /// Sorumluluklar:
    ///   • Retry + exponential backoff + jitter (transient: HttpRequestException, timeout).
    ///   • Transient HTTP durumları: 408, 429, 500, 502, 503, 504. 429'da Retry-After'a saygı.
    ///   • Per-attempt timeout (linked CTS). Dıştaki HttpClient.Timeout Infinite bırakılır;
    ///     toplam süreyi bu handler bounded tutar (attempt sayısı × per-try timeout + backoff).
    ///   • CancellationToken tam taşınır — çağıran iptali retry etmez.
    ///   • Response introspection: 2xx gövdesini buffer'lar, api-football zarfındaki
    ///     `errors` ve `results` alanlarını okur; hata/boş sonucu LOG'lar (sessiz hata yok),
    ///     sonra gövdeyi downstream deserialize için aynen geçirir.
    ///
    /// Provider return sözleşmesi (boş liste / null) DEĞİŞTİRİLMEZ — job'lar buna bağlı;
    /// bu katman yalnız görünürlük + otomatik retry ekler.
    /// </summary>
    public sealed class ApiFootballResilienceHandler : DelegatingHandler
    {
        private const int MaxRetries = 3;                                  // toplam 4 deneme
        private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan MaxDelay  = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan PerAttemptTimeout = TimeSpan.FromSeconds(15);

        private readonly ILogger<ApiFootballResilienceHandler> _logger;

        public ApiFootballResilienceHandler(ILogger<ApiFootballResilienceHandler> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Not: api-football anahtarı HEADER'da (x-apisports-key), URL'de DEĞİL — URL log'u güvenli.
            var url = request.RequestUri;
            HttpResponseMessage? response = null;

            for (var attempt = 1; attempt <= MaxRetries + 1; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(PerAttemptTimeout);

                try
                {
                    response = await base.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Gerçek çağıran iptali — retry etme, yukarı fırlat.
                    throw;
                }
                catch (OperationCanceledException ex) when (attempt <= MaxRetries)
                {
                    // Per-attempt timeout (çağıran iptal etmedi) — transient say, retry et.
                    var delay = ComputeDelay(attempt, null);
                    _logger.LogWarning(ex,
                        "[API-FOOTBALL] timeout (deneme {Attempt}/{Max}) → {Delay}ms sonra retry {Url}",
                        attempt, MaxRetries + 1, (int)delay.TotalMilliseconds, url);
                    await SafeDelay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                catch (OperationCanceledException ex)
                {
                    // Retry hakkı bitti.
                    _logger.LogError(ex,
                        "[API-FOOTBALL] timeout — retry hakkı bitti ({Max} deneme) {Url}",
                        MaxRetries + 1, url);
                    throw;
                }
                catch (HttpRequestException ex) when (attempt <= MaxRetries)
                {
                    var delay = ComputeDelay(attempt, null);
                    _logger.LogWarning(ex,
                        "[API-FOOTBALL] transport hatası (deneme {Attempt}/{Max}) → {Delay}ms sonra retry {Url}",
                        attempt, MaxRetries + 1, (int)delay.TotalMilliseconds, url);
                    await SafeDelay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Transient HTTP durumu → retry.
                if (IsTransient(response.StatusCode) && attempt <= MaxRetries)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta;
                    var delay = ComputeDelay(attempt, retryAfter);
                    _logger.LogWarning(
                        "[API-FOOTBALL] HTTP {Status} (deneme {Attempt}/{Max}) → {Delay}ms sonra retry {Url}",
                        (int)response.StatusCode, attempt, MaxRetries + 1, (int)delay.TotalMilliseconds, url);
                    response.Dispose();
                    await SafeDelay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                break; // başarı, non-transient hata, veya retry hakkı bitti
            }

            if (response == null)
                throw new HttpRequestException($"[API-FOOTBALL] response alınamadı: {url}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "[API-FOOTBALL] başarısız HTTP {Status} ({Reason}) {Url}",
                    (int)response.StatusCode, response.ReasonPhrase, url);
                return response;
            }

            // 2xx — api-football zarfını görünür kıl (errors[] / results). Asla pipeline'ı bozma.
            await InspectEnvelopeAsync(response, url, cancellationToken).ConfigureAwait(false);
            return response;
        }

        private static bool IsTransient(HttpStatusCode status) => status switch
        {
            HttpStatusCode.RequestTimeout        => true, // 408
            (HttpStatusCode)429                  => true, // Too Many Requests
            HttpStatusCode.InternalServerError   => true, // 500
            HttpStatusCode.BadGateway            => true, // 502
            HttpStatusCode.ServiceUnavailable    => true, // 503
            HttpStatusCode.GatewayTimeout        => true, // 504
            _                                    => false
        };

        private static TimeSpan ComputeDelay(int attempt, TimeSpan? retryAfter)
        {
            // Exponential: base * 2^(attempt-1), MaxDelay ile sınırlı, + jitter.
            var exp = TimeSpan.FromMilliseconds(
                Math.Min(MaxDelay.TotalMilliseconds, BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)));
            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
            var computed = exp + jitter;

            // 429 Retry-After varsa ona saygı göster (daha büyük olanı seç).
            if (retryAfter is TimeSpan ra && ra > computed)
                return ra;
            return computed;
        }

        private static async Task SafeDelay(TimeSpan delay, CancellationToken ct)
        {
            try { await Task.Delay(delay, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { /* çağıran iptali — döngü başında yakalanır */ }
        }

        /// <summary>
        /// 2xx gövdesini buffer'lar, api-football zarfını okur, hata/boş sonucu loglar,
        /// gövdeyi downstream için aynen geri koyar. Introspection asla exception fırlatmaz.
        /// </summary>
        private async Task InspectEnvelopeAsync(
            HttpResponseMessage response, Uri? url, CancellationToken ct)
        {
            try
            {
                if (response.Content == null) return;

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType != null && !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
                    return; // yalnız JSON zarfını denetle

                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                // Gövdeyi downstream (GetFromJsonAsync) için yeniden yerleştir.
                response.Content = new StringContent(body, Encoding.UTF8, "application/json");

                if (string.IsNullOrWhiteSpace(body)) return;

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return;

                // errors: [] (hata yok) | { ... } (hata) | ["msg"] (hata)
                if (root.TryGetProperty("errors", out var errors))
                {
                    var hasError =
                        (errors.ValueKind == JsonValueKind.Array  && errors.GetArrayLength() > 0) ||
                        (errors.ValueKind == JsonValueKind.Object && HasAnyProperty(errors));

                    if (hasError)
                    {
                        _logger.LogError(
                            "[API-FOOTBALL] response errors[] DOLU (plan/kota/parametre hatası olası) {Url} → {Errors}",
                            url, errors.GetRawText());
                        return;
                    }
                }

                // results == 0 → boş sonuç (sessiz olmasın; çağıran ayırt edebilsin diye loglanır)
                if (root.TryGetProperty("results", out var results) &&
                    results.ValueKind == JsonValueKind.Number &&
                    results.TryGetInt32(out var count) &&
                    count == 0)
                {
                    _logger.LogInformation("[API-FOOTBALL] boş sonuç (results=0) {Url}", url);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[API-FOOTBALL] response introspection atlandı {Url}", url);
            }
        }

        private static bool HasAnyProperty(JsonElement obj)
        {
            foreach (var _ in obj.EnumerateObject()) return true;
            return false;
        }
    }
}
