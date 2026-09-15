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

    /// <summary>Tek robots.txt kuralı (RFC 9309).</summary>
    public sealed record RobotsRule(bool Allow, string Pattern);

    /// <summary>
    /// ROBOTS.TXT POLİTİKASI — RFC 9309, İSTİSNASIZ (15.09.2026 kullanıcı kararı).
    ///
    /// Eskiden YouTube <c>/feeds/</c>, oEmbed ve Wikidata "yayımlanmış API" sayılıp kuraldan muaf tutuluyordu. Oysa
    /// youtube.com/robots.txt <c>Disallow: /feeds/videos.xml</c>, query.wikidata.org/robots.txt <c>Disallow: /sparql</c>
    /// yazıyor. Artık yalnız robots.txt dosyasının kendisi muaftır; her istek aynı kuralla sınanır.
    ///
    /// Durum kodu anlamı (RFC 9309 §2.3.1): 2xx → kurallar uygulanır; 4xx (429 hariç) → "erişilemez değil, yok"
    /// sayılır ve kısıt yoktur; 5xx / ağ hatası / 429 → "ulaşılamaz", tamamı yasak kabul edilir.
    /// Eşleşme: grup "formax" içeren user-agent varsa o, yoksa "*"; kurallar arasında EN UZUN eşleşen kazanır,
    /// eşit uzunlukta Allow üstündür; <c>*</c> joker, sondaki <c>$</c> satır sonu.
    /// </summary>
    public sealed class RobotsTxtPolicy
    {
        public enum RobotsState { Parsed, NoRestrictions, Unreachable }

        private readonly ConcurrentDictionary<string, (DateTime At, IReadOnlyList<RobotsRule> Rules, RobotsState State)> _cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Kuraldan muaf tek yol: robots.txt dosyasının kendisi.</summary>
        public static bool IsRobotsFile(Uri uri) => uri.AbsolutePath.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// KOD DÜZEYİ YASAK — robots.txt okunamasa bile (önbellek, test, hata) YouTube kanal akışına istek çıkmaz.
        /// Kullanıcı kararı: YouTube RSS ve YouTube Data API kullanılmaz.
        /// </summary>
        public static bool IsForbiddenByProductRule(Uri uri)
            => uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase)
               && (uri.AbsolutePath.StartsWith("/feeds/", StringComparison.OrdinalIgnoreCase)
                   || uri.AbsolutePath.StartsWith("/youtubei/", StringComparison.OrdinalIgnoreCase)
                   || uri.AbsolutePath.StartsWith("/results", StringComparison.OrdinalIgnoreCase))
               || uri.Host.Equals("www.googleapis.com", StringComparison.OrdinalIgnoreCase)
                   && uri.AbsolutePath.StartsWith("/youtube/", StringComparison.OrdinalIgnoreCase);

        /// <summary>Kural metninden bu istemciye uygulanacak grubun kurallarını çıkarır.</summary>
        public static List<RobotsRule> ParseRules(string robots)
        {
            var groups = new List<(List<string> Agents, List<RobotsRule> Rules)>();
            (List<string> Agents, List<RobotsRule> Rules)? current = null;
            var lastWasAgent = false;
            foreach (var raw in (robots ?? string.Empty).Split('\n'))
            {
                var line = raw.Split('#')[0].Trim();
                if (line.Length == 0) continue;
                var idx = line.IndexOf(':');
                if (idx <= 0) continue;
                var key = line[..idx].Trim().ToLowerInvariant();
                var value = line[(idx + 1)..].Trim();
                if (key == "user-agent")
                {
                    if (!lastWasAgent || current == null)
                    {
                        current = (new List<string>(), new List<RobotsRule>());
                        groups.Add(current.Value);
                    }
                    current.Value.Agents.Add(value.ToLowerInvariant());
                    lastWasAgent = true;
                    continue;
                }
                lastWasAgent = false;
                if (current == null) continue;
                if (key == "allow" && value.Length > 0) current.Value.Rules.Add(new RobotsRule(true, value));
                else if (key == "disallow" && value.Length > 0) current.Value.Rules.Add(new RobotsRule(false, value));
            }

            var formax = groups.Where(g => g.Agents.Any(a => a.Contains("formax", StringComparison.Ordinal))).SelectMany(g => g.Rules).ToList();
            if (formax.Count > 0 || groups.Any(g => g.Agents.Any(a => a.Contains("formax", StringComparison.Ordinal)))) return formax;
            return groups.Where(g => g.Agents.Contains("*")).SelectMany(g => g.Rules).ToList();
        }

        /// <summary>Geriye uyum: yalnız Disallow kalıpları.</summary>
        public static List<string> Parse(string robots) => ParseRules(robots).Where(r => !r.Allow).Select(r => r.Pattern).ToList();

        /// <summary>Geriye uyum: yalnız Disallow listesiyle karar.</summary>
        public static bool Allowed(IEnumerable<string> disallow, string path)
            => Allowed(disallow.Select(d => new RobotsRule(false, d)).ToList(), path);

        /// <summary>RFC 9309 kararı — en uzun eşleşen kural; eşitlikte Allow.</summary>
        public static bool Allowed(IReadOnlyList<RobotsRule> rules, string pathAndQuery)
        {
            RobotsRule? best = null;
            foreach (var r in rules)
            {
                if (!Matches(r.Pattern, pathAndQuery)) continue;
                if (best == null || r.Pattern.Length > best.Pattern.Length || (r.Pattern.Length == best.Pattern.Length && r.Allow))
                    best = r;
            }
            return best == null || best.Allow;
        }

        private static bool Matches(string pattern, string path)
        {
            var anchored = pattern.EndsWith("$", StringComparison.Ordinal);
            var body = anchored ? pattern[..^1] : pattern;
            var parts = body.Split('*');
            var pos = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (i == 0)
                {
                    if (!path.StartsWith(part, StringComparison.Ordinal)) return false;
                    pos = part.Length;
                    continue;
                }
                if (part.Length == 0) { if (i == parts.Length - 1) return true; continue; }
                var found = path.IndexOf(part, pos, StringComparison.Ordinal);
                if (found < 0) return false;
                pos = found + part.Length;
            }
            return !anchored || pos == path.Length;
        }

        /// <summary>HTTP durum kodundan RFC 9309 durumu.</summary>
        public static RobotsState StateFor(int? httpStatus)
            => httpStatus is >= 200 and < 300 ? RobotsState.Parsed
             : httpStatus is >= 400 and < 500 && httpStatus != 429 ? RobotsState.NoRestrictions
             : RobotsState.Unreachable;

        /// <summary>İstek robots.txt kurallarına göre yapılabilir mi? <paramref name="fetch"/> robots.txt'yi (durum, gövde) olarak döner.</summary>
        public async Task<bool> IsAllowedAsync(Uri uri, Func<Uri, CancellationToken, Task<(int? Status, string Body)>> fetch, CancellationToken ct)
        {
            if (IsForbiddenByProductRule(uri)) return false;
            if (IsRobotsFile(uri)) return true;
            var (rules, state) = await GetAsync(uri, fetch, ct).ConfigureAwait(false);
            return state switch
            {
                RobotsState.NoRestrictions => true,
                RobotsState.Unreachable => false,
                _ => Allowed(rules, uri.PathAndQuery)
            };
        }

        /// <summary>Host'un önbellekteki (24 sa) kural durumu; yoksa okunur.</summary>
        public async Task<(IReadOnlyList<RobotsRule> Rules, RobotsState State)> GetAsync(Uri uri,
            Func<Uri, CancellationToken, Task<(int? Status, string Body)>> fetch, CancellationToken ct)
        {
            var key = uri.Scheme + "://" + uri.Authority;
            if (_cache.TryGetValue(key, out var entry)
                && DateTime.UtcNow - entry.At < (entry.State == RobotsState.Unreachable ? TimeSpan.FromMinutes(30) : TimeSpan.FromHours(24)))
                return (entry.Rules, entry.State);
            var (status, body) = await fetch(new Uri(key + "/robots.txt"), ct).ConfigureAwait(false);
            var state = StateFor(status);
            entry = (DateTime.UtcNow, state == RobotsState.Parsed ? ParseRules(body) : new List<RobotsRule>(), state);
            _cache[key] = entry;
            return (entry.Rules, entry.State);
        }

        /// <summary>Katalog için özet: "Allowed" | "PartiallyDisallowed" | "Disallowed" | "Unreachable".</summary>
        public static string Summarize(IReadOnlyList<RobotsRule> rules, RobotsState state)
            => state == RobotsState.Unreachable ? "Unreachable"
             : state == RobotsState.NoRestrictions || rules.All(r => r.Allow) ? "Allowed"
             : !Allowed(rules, "/") ? "Disallowed" : "PartiallyDisallowed";
    }

    /// <summary>
    /// "postmatch-video" ve "video-source-discovery" istemcilerine takılan nezaket katmanı:
    /// ürün yasağı + robots.txt (RFC 9309) + host başına tek eşzamanlı istek ve asgari aralık + devre kesici +
    /// idempotent GET için sınırlı üstel geri çekilmeli yeniden deneme (5xx/ağ hatası; 429'da yeniden denenmez).
    /// </summary>
    public sealed class PoliteHttpHandler : DelegatingHandler
    {
        public const int MaxRetries = 2;
        private static readonly TimeSpan RetryBase = TimeSpan.FromSeconds(2);

        private readonly HostRateLimiter _limiter;
        private readonly RobotsTxtPolicy _robots;
        private readonly ILogger<PoliteHttpHandler> _log;

        public PoliteHttpHandler(HostRateLimiter limiter, RobotsTxtPolicy robots, ILogger<PoliteHttpHandler> log)
        {
            _limiter = limiter; _robots = robots; _log = log;
        }

        /// <summary>Süreç boyunca robots/ürün kuralıyla engellenen istek sayısı (teşhis).</summary>
        public static long BlockedByRobots;

        public const int MaxRedirects = 5;

        /// <summary>
        /// Yönlendirmeler burada izlenir (birincil işleyicide otomatik yönlendirme KAPALI): her atlamada ürün kuralı, robots.txt ve
        /// host sınırı yeniden uygulanır; https'ten http'ye düşülmez. Son yanıtın RequestMessage'ı son adresi taşır.
        /// </summary>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var current = request;
            for (var hop = 0; ; hop++)
            {
                var response = await SendOneAsync(current, ct).ConfigureAwait(false);
                var code = (int)response.StatusCode;
                if (code is not (301 or 302 or 303 or 307 or 308) || hop >= MaxRedirects || response.Headers.Location == null
                    || current.Method != HttpMethod.Get)
                    return response;
                var next = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(current.RequestUri!, response.Headers.Location);
                if (current.RequestUri!.Scheme == Uri.UriSchemeHttps && next.Scheme != Uri.UriSchemeHttps) return response;
                response.Dispose();
                var follow = new HttpRequestMessage(HttpMethod.Get, next);
                foreach (var h in request.Headers) follow.Headers.TryAddWithoutValidation(h.Key, h.Value);
                current = follow;
            }
        }

        private async Task<HttpResponseMessage> SendOneAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            if (RobotsTxtPolicy.IsForbiddenByProductRule(uri))
            {
                Interlocked.Increment(ref BlockedByRobots);
                return new HttpResponseMessage((HttpStatusCode)451) { RequestMessage = request, ReasonPhrase = "forbidden by product rule (YouTube RSS/Data API)" };
            }

            var allowed = await _robots.IsAllowedAsync(uri, async (robotsUri, c) =>
            {
                using var lease = await _limiter.AcquireAsync(robotsUri.Host, c).ConfigureAwait(false);
                if (lease == null) return (null, string.Empty);
                try
                {
                    // Asıl isteğin User-Agent'ı taşınır: UA'sız istek bazı sitelerde 503 döner (ölçüldü: premierleague.com),
                    // bu da robots.txt'yi "ulaşılamaz" gösterip siteyi tümden kapatıyordu.
                    using var robotsRequest = new HttpRequestMessage(HttpMethod.Get, robotsUri);
                    foreach (var ua in request.Headers.UserAgent) robotsRequest.Headers.UserAgent.Add(ua);
                    using var res = await base.SendAsync(robotsRequest, c).ConfigureAwait(false);
                    var body = res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync(c).ConfigureAwait(false) : string.Empty;
                    return ((int?)res.StatusCode, body);
                }
                catch (Exception) when (!c.IsCancellationRequested) { return (null, string.Empty); }
            }, ct).ConfigureAwait(false);

            if (!allowed)
            {
                Interlocked.Increment(ref BlockedByRobots);
                return new HttpResponseMessage((HttpStatusCode)451) { RequestMessage = request, ReasonPhrase = "robots.txt disallow or unreachable" };
            }

            for (var attempt = 0; ; attempt++)
            {
                using (var gate = await _limiter.AcquireAsync(uri.Host, ct).ConfigureAwait(false))
                {
                    if (gate == null)
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request, ReasonPhrase = "circuit-open" };

                    HttpResponseMessage? response = null;
                    try
                    {
                        using var copy = attempt == 0 ? null : Clone(request);
                        response = await base.SendAsync(copy ?? request, ct).ConfigureAwait(false);
                        var code = (int)response.StatusCode;
                        var failure = code == 429 || code >= 500;
                        _limiter.RecordResult(uri.Host, !failure);
                        if (code < 500 || attempt >= MaxRetries || request.Method != HttpMethod.Get) return response;
                        response.Dispose();
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                    {
                        _limiter.RecordResult(uri.Host, false);
                        _log.LogWarning("[VIDEO-HTTP] {Host} istek hatasi: {Type} (deneme {Attempt})", uri.Host, ex.GetType().Name, attempt + 1);
                        if (attempt >= MaxRetries || request.Method != HttpMethod.Get) throw;
                    }
                }
                // Üstel geri çekilme: 2 sn, 4 sn. Devre açılırsa bir sonraki tur null gate ile döner.
                await Task.Delay(TimeSpan.FromTicks(RetryBase.Ticks * (1L << attempt)), ct).ConfigureAwait(false);
            }
        }

        private static HttpRequestMessage Clone(HttpRequestMessage request)
        {
            var copy = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var h in request.Headers) copy.Headers.TryAddWithoutValidation(h.Key, h.Value);
            return copy;
        }
    }
}
