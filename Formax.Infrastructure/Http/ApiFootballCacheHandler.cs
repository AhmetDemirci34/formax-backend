using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Telemetry;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Http
{
    /// <summary>
    /// FORMAX VERİ KATMANI — api-football'a giden TEK kapı.
    ///
    /// Akış: L1 (memory) → L2 (kalıcı DB deposu) → gerçek HTTP.
    /// Tüm api-football HttpClient'larının EN DIŞ handler'ıdır; hiçbir servis/job bu kapıyı
    /// atlayamaz (Shadow A/B ve settlement zaten provider'a hiç çıkmaz — onlar DB'den okur).
    ///
    /// Getirdikleri:
    ///  • KALICI cache: restart L1'i siler, L2 kalır → yeniden başlatma tekrar istek DOĞURMAZ.
    ///  • SINGLE-FLIGHT: aynı anahtar için eşzamanlı N çağrı = 1 gerçek HTTP isteği.
    ///  • VERİ TÜRÜNE GÖRE TTL: bitmiş maç sonucu ~kalıcı, resmi kadro uzun, canlı kısa.
    ///  • GÜNLÜK BÜTÇE (emniyet kemeri): limit dolduğunda düşük öncelikli job'lar durur,
    ///    fikstür/sonuç zinciri son diliği kullanabilir.
    ///  • Gövde hatası (200 + errors) ASLA cache'lenmez.
    ///
    /// Model/olasılık/tahmin kodlarına dokunmaz — yalnız taşıma katmanıdır.
    /// </summary>
    public sealed class ApiFootballCacheHandler : DelegatingHandler
    {
        // Aynı anahtar için uçuşta olan tek istek (single-flight).
        private static readonly ConcurrentDictionary<string, Lazy<Task<CachedResponse>>> InFlight = new(StringComparer.Ordinal);

        /// <summary>Bütçenin son dilimini yalnız bu job'lar kullanabilir (fikstür/sonuç zinciri).</summary>
        private static readonly HashSet<string> CriticalCallers = new(StringComparer.OrdinalIgnoreCase)
        {
            "FixtureSyncJob", "LiveMatchIngestionJob"
        };

        private readonly IMemoryCache _l1;
        private readonly ApiFootballHttpCacheStore _l2;
        private readonly ApiFootballMetrics _metrics;
        private readonly IConfiguration _config;
        private readonly ILogger<ApiFootballCacheHandler> _logger;

        public ApiFootballCacheHandler(
            IMemoryCache l1, ApiFootballHttpCacheStore l2, ApiFootballMetrics metrics,
            IConfiguration config, ILogger<ApiFootballCacheHandler> logger)
        {
            _l1 = l1; _l2 = l2; _metrics = metrics; _config = config; _logger = logger;
        }

        /// <summary>
        /// Sağlayıcıdan veri SATIN ALAMAYAN çağıranlar — yalnız merkezi katmanın aldığını kullanırlar.
        /// GdpSyncJob zenginleştirmedir: FixtureSync'in aldığı fixtures verisini yeniden ısmarlaması
        /// ölçüldü (bir günde 18 istek) ve kritik bütçeyi yiyordu.
        /// </summary>
        private HashSet<string> CacheOnlyCallers =>
            new(_config.GetSection("ApiFootball:CacheOnlyCallers").Get<string[]>() ?? new[] { "GdpSyncJob" },
                StringComparer.OrdinalIgnoreCase);

        private int DailyLimit => Math.Max(1, _config.GetValue("ApiFootball:DailyRequestLimit", 100));
        private int CriticalReserve => Math.Max(0, _config.GetValue("ApiFootball:CriticalReserve", 20));
        private bool Enabled => _config.GetValue("ApiFootball:PersistentCache:Enabled", true);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            if (!Enabled || request.Method != HttpMethod.Get || request.RequestUri == null)
                return await base.SendAsync(request, ct).ConfigureAwait(false);

            var family = ApiFootballEndpointFamily.Classify(request.RequestUri);
            var normalized = NormalizeKey(request.RequestUri);
            var cacheKey = family + "|" + normalized;

            // ── L1 ────────────────────────────────────────────────────────────────
            if (_l1.TryGetValue(cacheKey, out CachedResponse? l1) && l1 != null)
            {
                _metrics.RecordCacheHit();
                return l1.ToHttpResponse();
            }

            // ── L2 + gerçek istek (tek uçuş) ──────────────────────────────────────
            var lazy = InFlight.GetOrAdd(cacheKey, k => new Lazy<Task<CachedResponse>>(
                () => FetchAsync(k, family, normalized, request, ct), LazyThreadSafetyMode.ExecutionAndPublication));
            try
            {
                var result = await lazy.Value.ConfigureAwait(false);
                return result.ToHttpResponse();
            }
            finally
            {
                InFlight.TryRemove(cacheKey, out _);
            }
        }

        private async Task<CachedResponse> FetchAsync(
            string cacheKey, string family, string normalized, HttpRequestMessage request, CancellationToken ct)
        {
            // ── L2: kalıcı depo (restart-safe) ────────────────────────────────────
            var stored = await _l2.TryGetAsync(cacheKey, ct).ConfigureAwait(false);
            if (stored != null)
            {
                _metrics.RecordPersistentCacheHit();
                var fromStore = new CachedResponse(HttpStatusCode.OK, stored);
                _l1.Set(cacheKey, fromStore, TimeSpan.FromMinutes(15));
                return fromStore;
            }

            var caller = ApiFootballCallScope.Current;

            // ── CACHE-ONLY ÇAĞIRANLAR ─────────────────────────────────────────────
            // Bu job'lar sağlayıcıdan VERİ SATIN ALAMAZ; yalnız merkezi katmanın (FixtureSync'in)
            // zaten aldığı veriyi kullanır. Cache'te yoksa "veri yok" değil, sağlayıcı-hatası
            // biçiminde döner → çağıran sessizce boş sonuç yazmaz, bir sonraki turda tekrar bakar.
            if (CacheOnlyCallers.Contains(caller))
            {
                _metrics.RecordBudgetBlock(family);
                _logger.LogDebug(
                    "[AF-CACHE] {Caller} cache-only — {Family} için gerçek istek YAPILMADI (merkezi veri bekleniyor).",
                    caller, family);
                return new CachedResponse(HttpStatusCode.OK,
                    "{\"errors\":{\"formax_cache_only\":\"caller may not purchase provider data\"},\"results\":0,\"response\":[]}");
            }

            // ── GÜNLÜK BÜTÇE (emniyet kemeri) ─────────────────────────────────────
            var used = await _l2.GetDailyUsageAsync(ct).ConfigureAwait(false);
            if (used >= 0)
            {
                var ceiling = CeilingFor(caller, DailyLimit, CriticalReserve);
                if (used >= ceiling)
                {
                    _metrics.RecordBudgetBlock(family);
                    _logger.LogWarning(
                        "[AF-BUDGET] Günlük bütçe doldu ({Used}/{Limit}) — {Caller} için {Family} isteği YAPILMADI.",
                        used, ceiling, caller ?? "unattributed", family);
                    // Sağlayıcı gövde hatası biçiminde döner: mevcut guard'lar bunu "veri yok"
                    // sanmaz, istisnaya çevirir → sahte boş kayıt YAZILMAZ.
                    return new CachedResponse(HttpStatusCode.OK,
                        "{\"errors\":{\"formax_budget\":\"daily provider budget exhausted\"},\"results\":0,\"response\":[]}");
                }
            }

            // ── Gerçek HTTP ───────────────────────────────────────────────────────
            using var response = await base.SendAsync(request, ct).ConfigureAwait(false);
            var body = response.Content == null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            await _l2.IncrementDailyUsageAsync(ct).ConfigureAwait(false);

            var result = new CachedResponse(response.StatusCode, body);

            if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(body))
            {
                var (cacheable, ttl, count) = Evaluate(family, request.RequestUri!, body);
                if (cacheable)
                {
                    await _l2.SetAsync(cacheKey, family, normalized, body,
                        DateTime.UtcNow.Add(ttl), count, ct).ConfigureAwait(false);
                    _l1.Set(cacheKey, result, ttl < TimeSpan.FromMinutes(15) ? ttl : TimeSpan.FromMinutes(15));
                }
            }
            return result;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Anahtar normalizasyonu: parametre sırası anahtarı değiştirmesin.
        // ──────────────────────────────────────────────────────────────────────────
        public static string NormalizeKey(Uri uri)
        {
            var path = uri.AbsolutePath.Trim('/').ToLowerInvariant();
            var query = uri.Query.TrimStart('?');
            if (query.Length == 0) return path;
            var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => Uri.UnescapeDataString(p).Trim())
                .Where(p => p.Length > 0)
                .OrderBy(p => p, StringComparer.Ordinal);
            return path + "?" + string.Join("&", parts);
        }

        /// <summary>
        /// BÜTÇE TAVANI — çağıranın bu gün kaç isteğe kadar gidebileceği.
        ///
        /// Fikstür/sonuç zinciri (kritik) günlük limitin TAMAMINI kullanabilir; diğer işler
        /// (oran, kadro, oyuncu) son <c>criticalReserve</c> dilimine giremez. Böylece kota
        /// dolmaya yaklaşırken sonuç alımı hâlâ nefes alır.
        ///
        /// Karar burada durur ki davranışı ağ/DB olmadan sınanabilsin — <see cref="FetchAsync"/>
        /// bu metodu çağırır, ikinci bir kopya YOKTUR.
        /// </summary>
        public static int CeilingFor(string? caller, int dailyLimit, int criticalReserve)
            => CriticalCallers.Contains(caller ?? string.Empty)
                ? dailyLimit
                : Math.Max(0, dailyLimit - criticalReserve);

        /// <summary>
        /// VERİ TÜRÜNE GÖRE TTL — kör tek TTL yok. Gövde hatası varsa cache'lenmez.
        /// </summary>
        public static (bool cacheable, TimeSpan ttl, int resultCount) Evaluate(string family, Uri uri, string body)
        {
            int count = 0;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return (false, TimeSpan.Zero, 0);

                // Kota/plan/parametre hatası ASLA cache'lenmez.
                if (root.TryGetProperty("errors", out var errors) && HasError(errors))
                    return (false, TimeSpan.Zero, 0);

                if (root.TryGetProperty("response", out var resp) && resp.ValueKind == JsonValueKind.Array)
                    count = resp.GetArrayLength();

                switch (family)
                {
                    case "fixtures":
                        // Geçmiş bir GÜN tamamen bitmişse sonuç KESİNDİR → bir daha çekilmez.
                        if (IsPastDateQuery(uri) && count > 0 && AllFinished(resp))
                            return (true, TimeSpan.FromDays(30), count);
                        if (uri.Query.Contains("live=", StringComparison.OrdinalIgnoreCase))
                            return (true, TimeSpan.FromSeconds(60), count);
                        // MESAFEYE GÖRE TTL: pencerenin uzak günleri neredeyse hiç değişmez;
                        // her 6 saatlik turda 9 günü birden yeniden çekmek kotanın en büyük
                        // sessiz kaçağıydı. Bugün/yarın taze tutulur, uzak günler günde bir.
                        var days = DaysAhead(uri);
                        if (days is int d)
                            return (true,
                                d <= 1 ? TimeSpan.FromHours(5)      // bugün/yarın: taze kalmalı
                              : d <= 3 ? TimeSpan.FromHours(24)     // yakın günler: günde bir
                                       : TimeSpan.FromHours(48),    // uzak günler: iki günde bir
                                count);
                        return (true, TimeSpan.FromHours(6), count);

                    case "fixtures/lineups":
                        // Resmi kadro yayımlandıysa DEĞİŞMEZ → 7 gün. Henüz yayımlanmadıysa
                        // 20 dakikadan önce tekrar sorulmaz (5 dakikalık döngü bu ucu dövüyordu).
                        return count > 0
                            ? (true, TimeSpan.FromDays(7), count)
                            : (true, TimeSpan.FromMinutes(20), count);

                    // Sakatlık listesi gün içinde nadiren değişir; 6 saatlik TTL her fikstür için
                    // günde 4 istek demekti (LineupIngestionJob 5 dakikada bir tarıyor).
                    case "injuries":       return (true, TimeSpan.FromHours(12), count);
                    case "players":        return (true, TimeSpan.FromHours(24), count);
                    // Oran gün içinde oynar ama 6 saatlik TTL, 2 günlük pencere × sayfa sayısı ile
                    // çarpılınca kotanın en büyük ikinci kalemiydi.
                    case "odds":
                        // Maç uzaktaysa oran neredeyse hiç oynamaz → daha uzun TTL.
                        var oddsDays = DaysAhead(uri);
                        return (true, oddsDays is int od && od >= 2
                            ? TimeSpan.FromHours(24)
                            : TimeSpan.FromHours(12), count);
                    case "standings":
                    case "teams/statistics":
                    case "predictions":    return (true, TimeSpan.FromHours(6), count);
                    case "fixtures/events":
                    case "fixtures/statistics": return (true, TimeSpan.FromSeconds(60), count);
                    case "status":         return (true, TimeSpan.FromHours(1), count);
                    // Kadro/profil/teknik direktör/transfer/takım künyesi: yavaş değişen referans veri.
                    default:               return (true, TimeSpan.FromDays(7), count);
                }
            }
            catch
            {
                return (false, TimeSpan.Zero, count); // ayrıştırılamayan gövde cache'lenmez
            }
        }

        private static bool HasError(JsonElement errors) => errors.ValueKind switch
        {
            JsonValueKind.Object => errors.EnumerateObject().MoveNext(),
            JsonValueKind.Array => errors.GetArrayLength() > 0,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(errors.GetString()),
            _ => false
        };

        /// <summary>fixtures?date=… sorgusunun bugünden kaç gün ileride olduğu (yoksa null).</summary>
        private static int? DaysAhead(Uri uri)
        {
            var raw = ReadDateParam(uri);
            if (raw == null || !DateTime.TryParse(raw, out var d)) return null;
            return (int)(d.Date - DateTime.UtcNow.Date).TotalDays;
        }

        private static string? ReadDateParam(Uri uri)
        {
            var q = uri.Query;
            var idx = q.IndexOf("date=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var raw = q.Substring(idx + 5);
            var amp = raw.IndexOf('&');
            return amp >= 0 ? raw.Substring(0, amp) : raw;
        }

        private static bool IsPastDateQuery(Uri uri)
        {
            var q = uri.Query;
            var idx = q.IndexOf("date=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            var raw = q.Substring(idx + 5);
            var amp = raw.IndexOf('&');
            if (amp >= 0) raw = raw.Substring(0, amp);
            return DateTime.TryParse(raw, out var d) && d.Date < DateTime.UtcNow.Date;
        }

        private static bool AllFinished(JsonElement response)
        {
            if (response.ValueKind != JsonValueKind.Array) return false;
            foreach (var item in response.EnumerateArray())
            {
                if (!item.TryGetProperty("fixture", out var fx) ||
                    !fx.TryGetProperty("status", out var st) ||
                    !st.TryGetProperty("short", out var shortStatus)) return false;
                var s = shortStatus.GetString();
                if (s is not ("FT" or "AET" or "PEN" or "CANC" or "ABD" or "AWD" or "WO" or "PST")) return false;
            }
            return true;
        }

        /// <summary>Cache'lenmiş yanıt gövdesi; her tüketim için TAZE HttpResponseMessage üretir.</summary>
        internal sealed class CachedResponse
        {
            private readonly HttpStatusCode _status;
            private readonly string _body;
            public CachedResponse(HttpStatusCode status, string body) { _status = status; _body = body; }

            public HttpResponseMessage ToHttpResponse() => new(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
