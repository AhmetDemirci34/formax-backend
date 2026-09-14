using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>
    /// HOST BAZLI NEZAKET KURALLARI — dış kaynağa saldırmamak için tek yer.
    ///  • Host başına eşzamanlı TEK istek ve istekler arası en az aralık.
    ///  • Ardışık hatalarda devre kesici (üstel artan kapalı kalma süresi).
    ///  • robots.txt: yayımlanmış API uçları dışındaki sayfalar için Disallow kuralına uyulur.
    /// Durum süreç belleğindedir; restart sonrası yeniden öğrenilir (kalıcı olan keşif defteridir).
    /// </summary>
    public sealed class HostRateLimiter
    {
        public static readonly TimeSpan DefaultMinInterval = TimeSpan.FromMilliseconds(1500);
        private const int FailuresToOpen = 5;
        private static readonly TimeSpan BaseOpen = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan MaxOpen = TimeSpan.FromHours(2);

        private sealed class HostState
        {
            public readonly SemaphoreSlim Gate = new(1, 1);
            public DateTime NextAllowedUtc;
            public int ConsecutiveFailures;
            public int Opens;
            public DateTime OpenUntilUtc;
        }

        private readonly ConcurrentDictionary<string, HostState> _hosts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Func<DateTime> _clock;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;

        public HostRateLimiter() : this(() => DateTime.UtcNow, (t, ct) => Task.Delay(t, ct)) { }

        public HostRateLimiter(Func<DateTime> clock, Func<TimeSpan, CancellationToken, Task> delay)
        {
            _clock = clock; _delay = delay;
        }

        public static TimeSpan MinIntervalFor(string host)
            => host.EndsWith("wikidata.org", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromSeconds(2) : DefaultMinInterval;

        public bool IsOpen(string host, out DateTime until)
        {
            var s = _hosts.GetOrAdd(host, _ => new HostState());
            until = s.OpenUntilUtc;
            return s.OpenUntilUtc > _clock();
        }

        /// <summary>Host için sırayı bekler; devre açıksa beklemeden false döner.</summary>
        public async Task<IDisposable?> AcquireAsync(string host, CancellationToken ct)
        {
            var s = _hosts.GetOrAdd(host, _ => new HostState());
            if (s.OpenUntilUtc > _clock()) return null;
            await s.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var wait = s.NextAllowedUtc - _clock();
                if (wait > TimeSpan.Zero) await _delay(wait, ct).ConfigureAwait(false);
                s.NextAllowedUtc = _clock() + MinIntervalFor(host);
                return new Release(s.Gate);
            }
            catch
            {
                s.Gate.Release();
                throw;
            }
        }

        public void RecordResult(string host, bool success)
        {
            var s = _hosts.GetOrAdd(host, _ => new HostState());
            if (success) { s.ConsecutiveFailures = 0; return; }
            if (++s.ConsecutiveFailures < FailuresToOpen) return;
            s.ConsecutiveFailures = 0;
            var open = TimeSpan.FromTicks(Math.Min(MaxOpen.Ticks, BaseOpen.Ticks * (1L << Math.Min(s.Opens, 5))));
            s.Opens++;
            s.OpenUntilUtc = _clock() + open;
        }

        public IReadOnlyList<object> Snapshot()
            => _hosts.Select(kv => (object)new { host = kv.Key, openUntilUtc = kv.Value.OpenUntilUtc, failures = kv.Value.ConsecutiveFailures, opens = kv.Value.Opens }).ToList();

        private sealed class Release : IDisposable
        {
            private SemaphoreSlim? _gate;
            public Release(SemaphoreSlim gate) => _gate = gate;
            public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
        }
    }

    /// <summary>
    /// robots.txt önbelleği (24 sa). Yayımlanmış API uçları (YouTube RSS/oEmbed, Wikidata API/SPARQL) bu
    /// kurala tabi değildir; kulüp/lig/yayıncı SAYFALARI tabidir. robots.txt okunamazsa sayfa ALINMAZ.
    /// </summary>
    public sealed class RobotsTxtPolicy
    {
        private readonly ConcurrentDictionary<string, (DateTime At, List<string> Disallow, bool Readable)> _cache = new(StringComparer.OrdinalIgnoreCase);

        public static bool IsPublishedApi(Uri uri)
            => (uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase)
                    && (uri.AbsolutePath.StartsWith("/feeds/", StringComparison.OrdinalIgnoreCase)
                        || uri.AbsolutePath.StartsWith("/oembed", StringComparison.OrdinalIgnoreCase)))
               || uri.Host.EndsWith("wikidata.org", StringComparison.OrdinalIgnoreCase)
               || uri.AbsolutePath.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase);

        /// <summary>Kural metninden "*" grubunun Disallow öneklerini çıkarır.</summary>
        public static List<string> Parse(string robots)
        {
            var result = new List<string>();
            var applies = false;
            foreach (var raw in (robots ?? string.Empty).Split('\n'))
            {
                var line = raw.Split('#')[0].Trim();
                if (line.Length == 0) continue;
                var idx = line.IndexOf(':');
                if (idx <= 0) continue;
                var key = line[..idx].Trim().ToLowerInvariant();
                var value = line[(idx + 1)..].Trim();
                if (key == "user-agent") applies = value == "*" || value.Contains("formax", StringComparison.OrdinalIgnoreCase);
                else if (key == "disallow" && applies && value.Length > 0) result.Add(value);
            }
            return result;
        }

        public static bool Allowed(IEnumerable<string> disallow, string path)
            => !disallow.Any(d => d == "/" || path.StartsWith(d.TrimEnd('*'), StringComparison.Ordinal));

        public async Task<bool> IsAllowedAsync(Uri uri, Func<Uri, CancellationToken, Task<(bool Ok, string Body)>> fetch, CancellationToken ct)
        {
            if (IsPublishedApi(uri)) return true;
            var key = uri.Scheme + "://" + uri.Host;
            if (!_cache.TryGetValue(key, out var entry) || DateTime.UtcNow - entry.At > TimeSpan.FromHours(24))
            {
                var (ok, body) = await fetch(new Uri(key + "/robots.txt"), ct).ConfigureAwait(false);
                entry = (DateTime.UtcNow, ok ? Parse(body) : new List<string>(), ok);
                _cache[key] = entry;
            }
            return entry.Readable && Allowed(entry.Disallow, uri.AbsolutePath);
        }
    }

    /// <summary>"postmatch-video" ve "video-source-discovery" istemcilerine takılan nezaket katmanı.</summary>
    public sealed class PoliteHttpHandler : DelegatingHandler
    {
        private readonly HostRateLimiter _limiter;
        private readonly RobotsTxtPolicy _robots;
        private readonly ILogger<PoliteHttpHandler> _log;

        public PoliteHttpHandler(HostRateLimiter limiter, RobotsTxtPolicy robots, ILogger<PoliteHttpHandler> log)
        {
            _limiter = limiter; _robots = robots; _log = log;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            var allowed = await _robots.IsAllowedAsync(uri, async (robotsUri, c) =>
            {
                using var lease = await _limiter.AcquireAsync(robotsUri.Host, c).ConfigureAwait(false);
                if (lease == null) return (false, string.Empty);
                try
                {
                    using var res = await base.SendAsync(new HttpRequestMessage(HttpMethod.Get, robotsUri), c).ConfigureAwait(false);
                    if (res.StatusCode == HttpStatusCode.NotFound) return (true, string.Empty);   // robots yok = kısıt yok
                    return (res.IsSuccessStatusCode, res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync(c).ConfigureAwait(false) : string.Empty);
                }
                catch (Exception) when (!c.IsCancellationRequested) { return (false, string.Empty); }
            }, ct).ConfigureAwait(false);

            if (!allowed)
                return new HttpResponseMessage((HttpStatusCode)451) { RequestMessage = request, ReasonPhrase = "robots.txt disallow or unreadable" };

            using var gate = await _limiter.AcquireAsync(uri.Host, ct).ConfigureAwait(false);
            if (gate == null)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request, ReasonPhrase = "circuit-open" };

            try
            {
                var response = await base.SendAsync(request, ct).ConfigureAwait(false);
                var failure = (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;
                _limiter.RecordResult(uri.Host, !failure);
                return response;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                _limiter.RecordResult(uri.Host, false);
                _log.LogWarning("[VIDEO-HTTP] {Host} istek hatasi: {Type}", uri.Host, ex.GetType().Name);
                throw;
            }
        }
    }
}
