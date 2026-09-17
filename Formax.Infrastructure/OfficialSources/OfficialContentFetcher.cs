using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.OfficialSources
{
    /// <summary>İndirici sınırları.</summary>
    public sealed class OfficialFetcherOptions
    {
        /// <summary>Gövde üst sınırı. Ölçülen en büyük resmî cevap ~1 MB (Bundesliga sayfası).</summary>
        public int MaxResponseBytes { get; set; } = 4 * 1024 * 1024;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

        public int MaxRedirects { get; set; } = 3;

        /// <summary>Boşsa kayıt defterinin doğrulanmış host'ları kullanılır (üretim).</summary>
        public IReadOnlySet<string>? AllowedHostsOverride { get; set; }
    }

    /// <summary>
    /// RESMÎ İÇERİK İNDİRİCİ — bkz. <see cref="IOfficialContentFetcher"/>. Tur başına bir kez
    /// oluşturulur (scoped); tur hafızası bu örnektedir, kalıcı tekillik defterdedir.
    /// </summary>
    public sealed class OfficialContentFetcher : IOfficialContentFetcher
    {
        public const string HttpClientName = "official-sources";
        public const string UserAgent = "FORMAX-OfficialSources/1.0";

        private readonly HttpClient _http;
        private readonly OfficialSourceStore _store;
        private readonly OfficialHostRateLimiter _limiter;
        private readonly IOfficialAddressResolver _resolver;
        private readonly OfficialFetcherOptions _options;
        private readonly ILogger<OfficialContentFetcher> _log;
        private readonly Func<DateTime> _utcNow;

        /// <summary>robots.txt politikası (RFC 9309). Üretimde DI verir; null ise (yalnız eski birim testleri) denetim yapılmaz.</summary>
        private readonly Formax.Infrastructure.PostMatch.RobotsTxtPolicy? _robots;

        /// <summary>Tur hafızası: (tur|adres) → sonuç. Aynı tur aynı adresi bir kez indirir.</summary>
        private readonly Dictionary<string, OfficialFetchResult> _roundMemo = new(StringComparer.Ordinal);

        static OfficialContentFetcher()
        {
            // windows-1254 (TFF) gibi kod sayfaları için.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public OfficialContentFetcher(
            IHttpClientFactory httpFactory,
            OfficialSourceStore store,
            OfficialHostRateLimiter limiter,
            IOfficialAddressResolver resolver,
            OfficialFetcherOptions options,
            ILogger<OfficialContentFetcher> log,
            Formax.Infrastructure.PostMatch.RobotsTxtPolicy? robots = null)
            : this(httpFactory.CreateClient(HttpClientName), store, limiter, resolver, options, log, null, robots) { }

        /// <summary>Test yapıcısı: gerçek ağ yerine verilen istemci ve saat.</summary>
        public OfficialContentFetcher(
            HttpClient http,
            OfficialSourceStore store,
            OfficialHostRateLimiter limiter,
            IOfficialAddressResolver resolver,
            OfficialFetcherOptions options,
            ILogger<OfficialContentFetcher> log,
            Func<DateTime>? utcNow,
            Formax.Infrastructure.PostMatch.RobotsTxtPolicy? robots = null)
        {
            _robots = robots;
            _http = http;
            _store = store;
            _limiter = limiter;
            _resolver = resolver;
            _options = options;
            _log = log;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>Bu turda ağa gerçekten çıkılan istek sayısı (teşhis).</summary>
        public int NetworkRequestCount { get; private set; }

        private bool HostAllowed(string host)
            => _options.AllowedHostsOverride != null
                ? _options.AllowedHostsOverride.Contains(host)
                : OfficialSourceRegistry.IsAllowedHost(host);

        public static string Hash(string value)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        public async Task<OfficialFetchResult> FetchAsync(OfficialFetchRequest request, CancellationToken ct = default)
        {
            var started = Stopwatch.StartNew();
            var now = _utcNow();

            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
                return await FailAsync(request, null, OfficialFetchOutcomes.NotHttps, null, started, now, ct);
            if (uri.Scheme != Uri.UriSchemeHttps)
                return await FailAsync(request, uri, OfficialFetchOutcomes.NotHttps, null, started, now, ct);
            if (!HostAllowed(uri.Host))
                return await FailAsync(request, uri, OfficialFetchOutcomes.HostNotAllowed, null, started, now, ct);

            var urlHash = Hash(uri.AbsoluteUri);

            // ── TUR TEKİLLİĞİ — aynı tur aynı adresi bir kez indirir ────────────────
            if (request.RoundKey != null)
            {
                var memoKey = request.RoundKey + "|" + urlHash;
                if (_roundMemo.TryGetValue(memoKey, out var memo))
                    return await MemoAsync(request, uri, urlHash, memo, started, now, ct);

                // Restart sonrası aynı tur: defterde başarılı okuma varsa önbellekten verilir.
                if (await _store.FetchedInRoundAsync(request.RoundKey, urlHash, ct).ConfigureAwait(false))
                {
                    var cached = await _store.GetCacheAsync(urlHash, ct).ConfigureAwait(false);
                    if (cached != null)
                    {
                        var fromDb = new OfficialFetchResult(uri.AbsoluteUri, OfficialFetchOutcomes.RoundMemo, null,
                            cached.Body, cached.ContentHash, true, false, cached.ProcessedHash == cached.ContentHash, 0);
                        _roundMemo[memoKey] = fromDb;
                        return await MemoAsync(request, uri, urlHash, fromDb, started, now, ct);
                    }
                }
            }

            var cache = await _store.GetCacheAsync(urlHash, ct).ConfigureAwait(false);

            OfficialFetchResult result;
            try
            {
                result = await SendAsync(request, uri, urlHash, cache, started, now, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }

            if (request.RoundKey != null && result.Ok)
                _roundMemo[request.RoundKey + "|" + urlHash] = result;
            return result;
        }

        private async Task<OfficialFetchResult> SendAsync(
            OfficialFetchRequest request, Uri uri, string urlHash, OfficialSourceCacheEntry? cache,
            Stopwatch started, DateTime now, CancellationToken ct)
        {
            var current = uri;
            for (var hop = 0; hop <= _options.MaxRedirects; hop++)
            {
                // ── ADRES KORUMASI — her atlamada yeniden ───────────────────────────
                IPAddress[] addresses;
                try { addresses = await _resolver.ResolveAsync(current.Host, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    return await FailAsync(request, current, OfficialFetchOutcomes.NetworkError, null, started, now, ct, urlHash);
                }
                if (addresses.Length == 0 || addresses.Any(a => !OfficialNetworkGuard.IsPublic(a)))
                    return await FailAsync(request, current, OfficialFetchOutcomes.PrivateAddress, null, started, now, ct, urlHash);

                // ── ROBOTS.TXT — her atlamada; yasaklı yol İSTENMEZ, engel aşılmaz ─────────
                if (_robots != null && !await _robots.IsAllowedAsync(current, FetchRobotsAsync, ct).ConfigureAwait(false))
                    return await FailAsync(request, current, OfficialFetchOutcomes.RobotsDisallowed, null, started, now, ct, urlHash);

                using var gate = await _limiter.AcquireAsync(current.Host,
                    () => _store.LastNetworkRequestAsync(current.Host, ct), _utcNow, ct).ConfigureAwait(false);

                using var msg = new HttpRequestMessage(HttpMethod.Get, current);
                msg.Headers.UserAgent.ParseAdd(UserAgent);
                msg.Headers.Accept.ParseAdd(request.Accept);
                // Koşullu GET yalnız ilk adreste (önbellek anahtarı o adrestir).
                if (hop == 0 && cache != null)
                {
                    if (!string.IsNullOrWhiteSpace(cache.ETag))
                        msg.Headers.TryAddWithoutValidation("If-None-Match", cache.ETag);
                    if (!string.IsNullOrWhiteSpace(cache.LastModified))
                        msg.Headers.TryAddWithoutValidation("If-Modified-Since", cache.LastModified);
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(_options.Timeout);

                HttpResponseMessage res;
                try
                {
                    NetworkRequestCount++;
                    res = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    return await FailAsync(request, current, OfficialFetchOutcomes.Timeout, null, started, now, ct, urlHash);
                }
                catch (HttpRequestException ex) when (ex.InnerException is OfficialPrivateAddressException
                                                      || ex.InnerException?.InnerException is OfficialPrivateAddressException)
                {
                    return await FailAsync(request, current, OfficialFetchOutcomes.PrivateAddress, null, started, now, ct, urlHash);
                }
                catch (HttpRequestException)
                {
                    return await FailAsync(request, current, OfficialFetchOutcomes.NetworkError, null, started, now, ct, urlHash);
                }

                using (res)
                {
                    var code = (int)res.StatusCode;

                    // ── YÖNLENDİRME — host yeniden doğrulanır ─────────────────────
                    if (code is 301 or 302 or 303 or 307 or 308)
                    {
                        var location = res.Headers.Location;
                        if (location == null)
                            return await FailAsync(request, current, OfficialFetchOutcomes.RedirectRejected, code, started, now, ct, urlHash);
                        var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                        if (next.Scheme != Uri.UriSchemeHttps || !HostAllowed(next.Host))
                            return await FailAsync(request, next, OfficialFetchOutcomes.RedirectRejected, code, started, now, ct, urlHash);
                        current = next;
                        continue;
                    }

                    if (code == 304 && cache != null)
                    {
                        await _store.TouchValidatedAsync(urlHash, now, ct).ConfigureAwait(false);
                        var notModified = new OfficialFetchResult(uri.AbsoluteUri, OfficialFetchOutcomes.NotModified, code,
                            cache.Body, cache.ContentHash, true, false, cache.ProcessedHash == cache.ContentHash, 0);
                        var id = await LedgerAsync(request, uri, urlHash, notModified, cache.Body.Length, started, now, ct);
                        return notModified with { LedgerId = id };
                    }

                    if (code == 429)
                        return await FailAsync(request, current, OfficialFetchOutcomes.RateLimited, code, started, now, ct, urlHash);
                    if (code < 200 || code > 299)
                        return await FailAsync(request, current, OfficialFetchOutcomes.HttpError, code, started, now, ct, urlHash);

                    // ── GÖVDE — boyut sınırıyla ───────────────────────────────────
                    var declared = res.Content.Headers.ContentLength;
                    if (declared.HasValue && declared.Value > _options.MaxResponseBytes)
                        return await FailAsync(request, current, OfficialFetchOutcomes.TooLarge, code, started, now, ct, urlHash);

                    byte[] bytes;
                    try
                    {
                        bytes = await ReadLimitedAsync(res.Content, _options.MaxResponseBytes, timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        return await FailAsync(request, current, OfficialFetchOutcomes.Timeout, code, started, now, ct, urlHash);
                    }
                    if (bytes.Length > _options.MaxResponseBytes)
                        return await FailAsync(request, current, OfficialFetchOutcomes.TooLarge, code, started, now, ct, urlHash);

                    var body = Decode(bytes, request.Encoding ?? res.Content.Headers.ContentType?.CharSet);
                    var hash = Hash(body);
                    var changed = cache == null || cache.ContentHash != hash;

                    await _store.SaveCacheAsync(new OfficialSourceCacheEntry
                    {
                        UrlHash = urlHash,
                        Url = uri.AbsoluteUri,
                        SourceKey = request.SourceKey,
                        ETag = res.Headers.ETag?.ToString(),
                        LastModified = res.Content.Headers.LastModified?.ToString("R"),
                        ContentType = res.Content.Headers.ContentType?.MediaType,
                        ContentHash = hash,
                        Body = body,
                        FetchedAtUtc = now,
                        ValidatedAtUtc = now
                    }, ct).ConfigureAwait(false);

                    var ok = new OfficialFetchResult(uri.AbsoluteUri, OfficialFetchOutcomes.Fetched, code, body, hash,
                        false, changed, !changed && cache?.ProcessedHash == hash, 0);
                    var ledgerId = await LedgerAsync(request, uri, urlHash, ok, bytes.Length, started, now, ct);
                    return ok with { LedgerId = ledgerId };
                }
            }

            return await FailAsync(request, current, OfficialFetchOutcomes.RedirectRejected, null, started, now, ct, urlHash);
        }

        /// <summary>robots.txt okuması — aynı korumalı istemci; gövde sınırlı, zaman aşımı 10 sn.</summary>
        private async Task<(int? Status, string Body)> FetchRobotsAsync(Uri robotsUri, CancellationToken ct)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var current = robotsUri;
                // RFC 9309 §2.3.1.2: yönlendirme en fazla 5 kez izlenir. Yalnız HTTPS ve AYNI kayıtlı alan adı (ör. match.uefa.com →
                // www.uefa.com) içinde; başka alana giden yönlendirme = kural okunamadı = tam yasak (güvenli taraf).
                for (var hop = 0; hop <= 5; hop++)
                {
                    using var msg = new HttpRequestMessage(HttpMethod.Get, current);
                    msg.Headers.UserAgent.ParseAdd(UserAgent);
                    NetworkRequestCount++;
                    using var res = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                    var code = (int)res.StatusCode;
                    if (code is >= 300 and < 400)
                    {
                        var location = res.Headers.Location;
                        if (location == null || hop == 5) return (null, string.Empty);
                        var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                        if (next.Scheme != Uri.UriSchemeHttps || RegistrableDomain(next.Host) != RegistrableDomain(robotsUri.Host)) return (null, string.Empty);
                        current = next;
                        continue;
                    }
                    if (code < 200 || code > 299) return (code, string.Empty);
                    var bytes = await ReadLimitedAsync(res.Content, 512 * 1024, timeout.Token).ConfigureAwait(false);
                    return (code, Encoding.UTF8.GetString(bytes));
                }
                return (null, string.Empty);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return (null, string.Empty); }
            catch (HttpRequestException) { return (null, string.Empty); }
        }

        /// <summary>Son iki etiket (uefa.com). Kilitli kaynakların hiçbiri iki parçalı genel sonek (co.uk vb.) kullanmıyor.</summary>
        public static string RegistrableDomain(string host)
        {
            var parts = host.ToLowerInvariant().Split('.', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length <= 2 ? string.Join('.', parts) : parts[^2] + "." + parts[^1];
        }

        private static async Task<byte[]> ReadLimitedAsync(HttpContent content, int max, CancellationToken ct)
        {
            await using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), ct).ConfigureAwait(false)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > max) break; // sınır aşıldı — çağıran TooLarge der
            }
            return buffer.ToArray();
        }

        private static string Decode(byte[] bytes, string? charset)
        {
            Encoding enc = Encoding.UTF8;
            if (!string.IsNullOrWhiteSpace(charset))
            {
                try { enc = Encoding.GetEncoding(charset.Trim('"')); }
                catch (ArgumentException) { enc = Encoding.UTF8; }
            }
            return enc.GetString(bytes);
        }

        private async Task<OfficialFetchResult> MemoAsync(
            OfficialFetchRequest request, Uri uri, string urlHash, OfficialFetchResult memo,
            Stopwatch started, DateTime now, CancellationToken ct)
        {
            var hit = memo with { Outcome = OfficialFetchOutcomes.RoundMemo, HttpStatus = null, FromCache = true, ContentChanged = false };
            var id = await LedgerAsync(request, uri, urlHash, hit, 0, started, now, ct);
            return hit with { LedgerId = id };
        }

        private async Task<OfficialFetchResult> FailAsync(
            OfficialFetchRequest request, Uri? uri, string outcome, int? httpStatus,
            Stopwatch started, DateTime now, CancellationToken ct, string? urlHash = null)
        {
            var url = uri?.AbsoluteUri ?? request.Url;
            _log.LogWarning("[OFFICIAL] {Source} {Host}{Path} → {Outcome} ({Status})",
                request.SourceKey, uri?.Host ?? "-", uri?.AbsolutePath ?? "", outcome, httpStatus);
            var fail = new OfficialFetchResult(url, outcome, httpStatus, null, null, false, false, false, 0);
            var id = await LedgerAsync(request, uri, urlHash ?? Hash(url), fail, 0, started, now, ct);
            return fail with { LedgerId = id };
        }

        private async Task<long> LedgerAsync(
            OfficialFetchRequest request, Uri? uri, string urlHash, OfficialFetchResult result,
            int bytes, Stopwatch started, DateTime now, CancellationToken ct)
        {
            var url = uri?.AbsoluteUri ?? request.Url;
            return await _store.AppendLedgerAsync(new OfficialSourceFetch
            {
                SourceKey = request.SourceKey,
                Provider = request.Provider,
                Host = uri?.Host ?? string.Empty,
                UrlHash = urlHash,
                Url = url.Length > 1000 ? url[..1000] : url,
                Purpose = request.Purpose,
                RoundKey = request.RoundKey,
                MatchId = request.MatchId,
                RequestedAtUtc = now,
                HttpStatus = result.HttpStatus,
                Outcome = result.Outcome,
                CacheHit = result.FromCache,
                ContentHash = result.ContentHash,
                ContentChanged = result.ContentChanged,
                Bytes = bytes,
                DurationMs = (int)Math.Min(int.MaxValue, started.ElapsedMilliseconds)
            }, ct).ConfigureAwait(false);
        }

        public Task MarkProcessedAsync(string url, string contentHash, CancellationToken ct = default)
            => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                ? _store.MarkProcessedAsync(Hash(uri.AbsoluteUri), contentHash, ct)
                : Task.CompletedTask;

        public Task RecordDecisionAsync(long ledgerId, int candidates, int accepted, string decision, CancellationToken ct = default)
            => ledgerId <= 0 ? Task.CompletedTask : _store.UpdateDecisionAsync(ledgerId, candidates, accepted, decision, ct);
    }
}
