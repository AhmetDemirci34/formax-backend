using System;
using System.Collections.Generic;

namespace Formax.Application.Services.Live.Discovery
{
    /// <summary>
    /// FORMAX Live Data Engine — bir maç için global canlı-sinyal keşif sorgusu.
    /// News/Fixture Discovery ile aynı desen: FORMAX kendi veri katmanını besler,
    /// tek bir üçüncü-taraf API'ye bağımlı değildir.
    /// </summary>
    public sealed class LiveSignalQuery
    {
        public int MatchId { get; init; }
        public string HomeTeam { get; init; } = string.Empty;
        public string AwayTeam { get; init; } = string.Empty;
        public string League { get; init; } = string.Empty;
        public string Country { get; init; } = string.Empty;
        public DateTime KickoffUtc { get; init; }

        /// <summary>Provider'ların çalıştıracağı hazır arama ifadeleri.</summary>
        public IReadOnlyList<string> Queries { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Açık kaynaktan gelen ham canlı-sinyal adayı (haber başlığı / canlı metin parçası).
    /// Provider bunu döndürür; skor/olay çıkarımı orkestratörde yapılır.
    /// </summary>
    public sealed class LiveSignalCandidate
    {
        public string Provider { get; init; } = string.Empty;
        public string Publisher { get; init; } = string.Empty;
        public string Headline { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public DateTime PublishedUtc { get; init; }
    }

    /// <summary>Skor değişiminden türetilen canlı olay (gol vb.). Ingestion katmanı üretir.</summary>
    public sealed class DiscoveredLiveEvent
    {
        public string EventType { get; init; } = string.Empty;   // Goal, ...
        public int Minute { get; init; }
        public string? Team { get; init; }
        public string? Player { get; init; }
        public string? Detail { get; init; }
        public double ImpactScore { get; init; }
    }

    /// <summary>
    /// Bir maç için açık kaynaklardan uzlaştırılmış canlı görüntü. Skor/dakika güvenilir
    /// alındıysa <see cref="HasScore"/>=true. Güven düşükse job yazmaz (uydurma yok).
    /// </summary>
    public sealed class DiscoveredLiveSignal
    {
        public int MatchId { get; init; }
        public bool HasScore { get; init; }
        public int HomeScore { get; init; }
        public int AwayScore { get; init; }
        public int? Minute { get; init; }

        /// <summary>0..100 — kaç bağımsız kaynak aynı skoru doğruladı + tazelik.</summary>
        public int Confidence { get; init; }
        public int SourceCount { get; init; }

        public IReadOnlyList<DiscoveredLiveEvent> Events { get; init; } = Array.Empty<DiscoveredLiveEvent>();

        public static DiscoveredLiveSignal None(int matchId) => new() { MatchId = matchId, HasScore = false };
    }
}
