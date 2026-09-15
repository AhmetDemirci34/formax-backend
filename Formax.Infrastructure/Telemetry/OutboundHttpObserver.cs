using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace Formax.Infrastructure.Telemetry
{
    /// <summary>
    /// SÜREÇ İÇİ GİDEN HTTP GÖZLEMCİSİ — bütün HttpClient istekleri (API-Football, resmî siteler, oEmbed, Wikidata, LLM)
    /// .NET'in kendi <c>HttpHandlerDiagnosticListener</c> olaylarından kaydedilir; hangi GELEN isteğin (ör.
    /// <c>/api/matches/42847/detail</c>) yolunda çıktığı AsyncLocal ile işaretlenir. Amaç ölçümdür: "sayfa açılışında dış istek
    /// sıfır" iddiası kod okumasıyla değil bu kayıtla doğrulanır. İstek gövdesi/sorgu dizesi SAKLANMAZ (anahtar sızmaz).
    /// </summary>
    public sealed class OutboundHttpObserver : IObserver<DiagnosticListener>, IDisposable
    {
        public sealed record Entry(DateTime AtUtc, string Host, string Path, string? InboundPath, bool Background);

        /// <summary>Şu anki gelen isteğin yolu (middleware yazar). Arka plan işlerinde null.</summary>
        public static readonly AsyncLocal<string?> CurrentInbound = new();

        private readonly ConcurrentQueue<Entry> _entries = new();
        private readonly List<IDisposable> _subs = new();
        private IDisposable? _all;
        private long _total;
        private const int Max = 20_000;

        public long Total => Interlocked.Read(ref _total);

        public OutboundHttpObserver() => _all = DiagnosticListener.AllListeners.Subscribe(this);

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name != "HttpHandlerDiagnosticListener") return;
            lock (_subs) _subs.Add(listener.Subscribe(new Handler(this)));
        }

        public void OnError(Exception error) { }
        public void OnCompleted() { }

        private sealed class Handler : IObserver<KeyValuePair<string, object?>>
        {
            private readonly OutboundHttpObserver _owner;
            public Handler(OutboundHttpObserver owner) => _owner = owner;
            public void OnCompleted() { }
            public void OnError(Exception error) { }
            public void OnNext(KeyValuePair<string, object?> kv)
            {
                if (kv.Key != "System.Net.Http.HttpRequestOut.Start" && kv.Key != "System.Net.Http.Request") return;
                var request = kv.Value?.GetType().GetProperty("Request")?.GetValue(kv.Value) as HttpRequestMessage;
                var uri = request?.RequestUri;
                if (uri == null) return;
                if (uri.IsLoopback) return;   // yerel (localhost) çağrılar dış istek değildir
                var inbound = CurrentInbound.Value;
                _owner.Add(new Entry(DateTime.UtcNow, uri.Host, uri.AbsolutePath, inbound, inbound == null));
            }
        }

        private long _video;
        private Entry? _lastVideo;

        /// <summary>Süreç başından beri video niteliğindeki dış istek sayısı (YouTube, oEmbed, video sitemap).</summary>
        public long VideoRequestCount => Interlocked.Read(ref _video);

        /// <summary>Son video dış isteği; hiç yoksa null.</summary>
        public Entry? LastVideoRequest => _lastVideo;

        /// <summary>
        /// VİDEO İSTEĞİ SINIFI — YouTube ve YouTube-nocookie alanları, herhangi bir alandaki oEmbed yolu, video sitemap
        /// ve TRT SPOR video sitemap. Video özelliği kapalıyken bu sayaç 0 kalmalıdır.
        /// </summary>
        public static bool IsVideoRequest(string host, string path)
        {
            var h = host.ToLowerInvariant();
            var pth = (path ?? string.Empty).ToLowerInvariant();
            return h == "youtube.com" || h.EndsWith(".youtube.com") || h.EndsWith("youtube-nocookie.com") || h == "youtu.be"
                   || h.EndsWith("ytimg.com") || h.EndsWith("googlevideo.com")
                   || pth.Contains("oembed") || pth.Contains("sitemap_video") || pth.Contains("video-sitemap") || pth.Contains("videositemap")
                   || (pth.Contains("sitemap") && pth.Contains("video"));
        }

        private void Add(Entry e)
        {
            Interlocked.Increment(ref _total);
            if (IsVideoRequest(e.Host, e.Path)) { Interlocked.Increment(ref _video); _lastVideo = e; }
            _entries.Enqueue(e);
            while (_entries.Count > Max && _entries.TryDequeue(out _)) { }
        }

        public IReadOnlyList<Entry> Since(DateTime sinceUtc) => _entries.Where(e => e.AtUtc >= sinceUtc).ToList();

        public void Dispose()
        {
            _all?.Dispose(); _all = null;
            lock (_subs) { foreach (var s in _subs) s.Dispose(); _subs.Clear(); }
        }
    }
}
