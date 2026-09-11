using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Telemetry
{
    /// <summary>
    /// SINIRLI, SÜREÇ İÇİ KAYIT HALKASI — son N kaydı tutar, eskisi düşer.
    ///
    /// Kalıcılık yapılandırılmış log satırıyla sağlanır (her kayıt ayrıca ILogger'a
    /// alan alan yazılır); halka yalnız "şu an ne oluyor?" sorusunu uçtan okumak içindir.
    /// </summary>
    public abstract class BoundedRequestLog<T>
    {
        private readonly ConcurrentQueue<T> _items = new();
        private readonly int _capacity;
        private long _total;

        protected BoundedRequestLog(int capacity) => _capacity = Math.Max(10, capacity);

        /// <summary>Süreç başından beri yazılan toplam kayıt (halkadan düşenler dahil).</summary>
        public long Total => System.Threading.Interlocked.Read(ref _total);

        protected void Add(T item)
        {
            _items.Enqueue(item);
            System.Threading.Interlocked.Increment(ref _total);
            while (_items.Count > _capacity && _items.TryDequeue(out _)) { }
        }

        /// <summary>En yeniden eskiye.</summary>
        public IReadOnlyList<T> Snapshot() => _items.Reverse().ToList();
    }

    /// <summary>
    /// API-FOOTBALL İSTEK KAYDI — kapıdan (<see cref="Http.ApiFootballCacheHandler"/>) geçen
    /// HER istek için bir satır: gerçek HTTP, önbellekten dönen ve bütçe/cache-only
    /// kapısında durdurulan istekler ayrı ayrı görünür.
    ///
    /// SIR YOK: anahtar başlıkta taşınır ve buraya hiç gelmez; sorgu parametrelerinden
    /// yalnız izin listesindekiler (<see cref="SafeQueryKeys"/>) yazılır.
    /// </summary>
    public sealed class ApiFootballRequestLog : BoundedRequestLog<ApiFootballRequestLog.Entry>
    {
        /// <summary>Kayda girebilen sorgu parametreleri — başka hiçbiri yazılmaz.</summary>
        public static readonly IReadOnlySet<string> SafeQueryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "fixture", "id", "ids", "date", "league", "season", "team", "page", "live", "bookmaker", "from", "to", "last", "next"
        };

        public sealed record Entry(
            DateTime AtUtc,
            string Caller,
            string Endpoint,
            string SafeQuery,
            string? ExternalFixtureId,
            /// <summary>"L1Hit" | "L2Hit" | "Miss".</summary>
            string Cache,
            /// <summary>"Allowed" | "BudgetBlocked" | "CacheOnlyBlocked" | "NotApplicable".</summary>
            string Budget,
            /// <summary>Gerçek HTTP'de durum kodu; gerçek istek yapılmadıysa null.</summary>
            int? HttpStatus,
            /// <summary>"OK" | "ProviderError:&lt;anahtarlar&gt;" | "HttpError" | "NotRequested".</summary>
            string ProviderResult,
            bool RealRequest);

        private readonly ILogger<ApiFootballRequestLog> _logger;

        public ApiFootballRequestLog(ILogger<ApiFootballRequestLog> logger) : base(1000) => _logger = logger;

        public void Record(Entry e)
        {
            Add(e);
            _logger.LogInformation(
                "[AF-REQ] caller={Caller} endpoint={Endpoint} query={Query} fixture={Fixture} cache={Cache} budget={Budget} http={Http} result={Result} real={Real}",
                e.Caller, e.Endpoint, e.SafeQuery, e.ExternalFixtureId ?? "-", e.Cache, e.Budget,
                e.HttpStatus?.ToString() ?? "-", e.ProviderResult, e.RealRequest);
        }

        /// <summary>
        /// "fixture=123&season=2026" gibi GÜVENLİ sorgu metni ve varsa fikstür kimliği.
        /// İzin listesinde olmayan parametre (anahtar/token dahil) HİÇ yazılmaz.
        /// </summary>
        public static (string SafeQuery, string? FixtureId) Sanitize(Uri uri)
        {
            var query = uri?.Query?.TrimStart('?') ?? string.Empty;
            string? fixture = null;
            var kept = new List<string>();

            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                var key = Uri.UnescapeDataString(eq < 0 ? part : part[..eq]).Trim();
                var value = eq < 0 ? string.Empty : Uri.UnescapeDataString(part[(eq + 1)..]).Trim();
                if (!SafeQueryKeys.Contains(key)) continue;

                kept.Add(key + "=" + value);
                if (key.Equals("fixture", StringComparison.OrdinalIgnoreCase)) fixture = value;
                else if (fixture == null && key.Equals("ids", StringComparison.OrdinalIgnoreCase)) fixture = value;
            }

            // "fixtures?id=…" biçiminde fikstür kimliği "id" parametresindedir.
            var path = uri?.AbsolutePath?.Trim('/') ?? string.Empty;
            if (fixture == null && path.EndsWith("fixtures", StringComparison.OrdinalIgnoreCase))
            {
                var id = kept.FirstOrDefault(k => k.StartsWith("id=", StringComparison.OrdinalIgnoreCase));
                if (id != null) fixture = id[3..];
            }

            kept.Sort(StringComparer.Ordinal);
            return (string.Join("&", kept), fixture);
        }
    }

    /// <summary>
    /// VİDEO KEŞİF KAYDI — API-Football'dan TAMAMEN ayrı sayaç.
    ///
    /// İki tür satır tutar: sağlayıcının kaynağa yaptığı istek (<see cref="RequestEntry"/>)
    /// ve bir adayın kimlik/embed kapısındaki kararı (<see cref="VerdictEntry"/>).
    /// Sorgu dizesi HİÇ yazılmaz (YouTube Data API anahtarı sorguda taşınır); yalnız host
    /// ve yol kaydedilir.
    /// </summary>
    public sealed class VideoDiscoveryRequestLog
    {
        public sealed record RequestEntry(
            DateTime AtUtc,
            string Provider,
            string Host,
            string Path,
            int? MatchId,
            string? ExternalFixtureId,
            /// <summary>HTTP durum kodu, "cache" ya da hata türü.</summary>
            string Result,
            int CandidateCount);

        public sealed record VerdictEntry(
            DateTime AtUtc,
            string? Provider,
            int? MatchId,
            string? ExternalFixtureId,
            string SourceIdentifier,
            string ExternalVideoId,
            string Title,
            bool Accepted,
            string Status,
            string Reason);

        private sealed class Requests : BoundedRequestLog<RequestEntry> { public Requests() : base(1000) { } public void Put(RequestEntry e) => Add(e); }
        private sealed class Verdicts : BoundedRequestLog<VerdictEntry> { public Verdicts() : base(1000) { } public void Put(VerdictEntry e) => Add(e); }

        private readonly Requests _requests = new();
        private readonly Verdicts _verdicts = new();
        private readonly ILogger<VideoDiscoveryRequestLog> _logger;

        public VideoDiscoveryRequestLog(ILogger<VideoDiscoveryRequestLog> logger) => _logger = logger;

        public long TotalRequests => _requests.Total;
        public long TotalVerdicts => _verdicts.Total;

        public IReadOnlyList<RequestEntry> RequestSnapshot() => _requests.Snapshot();
        public IReadOnlyList<VerdictEntry> VerdictSnapshot() => _verdicts.Snapshot();

        /// <summary>Kaynağa yapılan isteği yazar. <paramref name="url"/>'nin sorgu kısmı ATILIR.</summary>
        public void RecordRequest(string provider, string url, int? matchId, string? externalFixtureId,
            string result, int candidateCount)
        {
            string host = "?", path = "?";
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                host = uri.Host;
                path = uri.AbsolutePath;
            }

            var e = new RequestEntry(DateTime.UtcNow, provider, host, path, matchId, externalFixtureId, result, candidateCount);
            _requests.Put(e);
            _logger.LogInformation(
                "[VIDEO-REQ] provider={Provider} host={Host} path={Path} match={MatchId} fixture={Fixture} result={Result} candidates={Count}",
                provider, host, path, matchId?.ToString() ?? "-", externalFixtureId ?? "-", result, candidateCount);
        }

        public void RecordVerdict(VerdictEntry e)
        {
            _verdicts.Put(e);
            _logger.LogInformation(
                "[VIDEO-VERDICT] provider={Provider} match={MatchId} source={Source} video={Video} accepted={Accepted} status={Status} reason={Reason}",
                e.Provider ?? "-", e.MatchId?.ToString() ?? "-", e.SourceIdentifier, e.ExternalVideoId,
                e.Accepted, e.Status, e.Reason);
        }
    }
}
