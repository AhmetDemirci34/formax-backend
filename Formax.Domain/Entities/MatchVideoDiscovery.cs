using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// RESMÎ VİDEO KAYNAK KATALOĞU — elle yazılmış kanal listesi yerine kalıcı ve otomatik büyüyen kayıt.
    ///
    /// Kaynaklar arka plan işiyle keşfedilir (Wikidata kulüp/lig kaydı: resmî YouTube kanal kimliği P2397,
    /// resmî site P856) ve ikinci bağımsız kanıtla doğrulanır (kulübün kendi sitesindeki kanal bağlantısı
    /// ya da kanal akışının yazar adı). Yalnız <c>Status = Verified</c> kayıtlar keşifte kullanılır.
    /// </summary>
    public sealed class OfficialVideoSourceRecord
    {
        public int Id { get; set; }
        /// <summary>Kararlı anahtar: "club:{teamId}", "league:{leagueId}", "seed:{key}".</summary>
        public string Key { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        /// <summary>"YouTube" | "Web".</summary>
        public string Platform { get; set; } = "YouTube";
        public string? YouTubeChannelId { get; set; }
        /// <summary>Web kaynaklarında resmî video sitemap/RSS adresi.</summary>
        public string? FeedUrl { get; set; }
        public int Tier { get; set; }
        public int? TeamId { get; set; }
        public string? ClubName { get; set; }
        /// <summary>Virgülle ayrılmış kanonik LeagueId listesi; boşsa kapsam sınırsız.</summary>
        public string? LeagueIds { get; set; }
        public bool AllowsInAppEmbed { get; set; }
        /// <summary>"Verified" | "Candidate" | "Rejected".</summary>
        public string Status { get; set; } = "Candidate";
        /// <summary>"Seed" | "Wikidata+OfficialSite" | "Wikidata+ChannelName" | "OfficialSite+ChannelName".</summary>
        public string DiscoveredVia { get; set; } = string.Empty;
        public string? WikidataId { get; set; }
        public string? OfficialWebsite { get; set; }
        public string VerificationEvidence { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? VerifiedAtUtc { get; set; }
        public DateTime? LastCheckedAtUtc { get; set; }
    }

    /// <summary>
    /// BİTMİŞ MAÇ VİDEO KEŞİF KUYRUĞU — maç başına tek satır, restart'ta kaybolmaz.
    /// Sayfa açılışı bu tabloya YAZMAZ; yalnız arka plan işi okur/yazar.
    /// </summary>
    public sealed class MatchVideoDiscoveryQueueItem
    {
        public int MatchId { get; set; }
        public string ExternalFixtureId { get; set; } = string.Empty;
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public int LeagueId { get; set; }
        public int? Season { get; set; }
        public DateTime KickoffUtc { get; set; }
        public DateTime EndUtc { get; set; }
        /// <summary>Searching | FullHighlightsAvailable | GoalClipsAvailable | NotAvailableYet | SourceBlocked | Failed.</summary>
        public string State { get; set; } = "Searching";
        /// <summary>Tamamlanmış (engellenmemiş) keşif turu sayısı.</summary>
        public int AttemptCount { get; set; }
        /// <summary>null = tekrar yok (tam özet bulundu).</summary>
        public DateTime? NextAttemptUtc { get; set; }
        public DateTime? LastAttemptUtc { get; set; }
        public string? LastOutcome { get; set; }
        public string? LastError { get; set; }
        /// <summary>Atomik rezervasyon: iki süreç aynı maçı aynı anda taramaz.</summary>
        public DateTime? LockedUntilUtc { get; set; }
        public string? LockOwner { get; set; }
        /// <summary>"Today" | "Yesterday" | "Backfill".</summary>
        public string EnqueueReason { get; set; } = "Backfill";
        public DateTime EnqueuedAtUtc { get; set; }
        public DateTime? FoundAtUtc { get; set; }
    }

    /// <summary>Keşif defteri — kaynak isteği ya da aday değerlendirmesi başına bir satır.</summary>
    public sealed class MatchVideoDiscoveryAttempt
    {
        public long Id { get; set; }
        public int MatchId { get; set; }
        public string ExternalFixtureId { get; set; } = string.Empty;
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public int LeagueId { get; set; }
        public int? Season { get; set; }
        public DateTime KickoffUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int AttemptNo { get; set; }
        public DateTime AttemptedAtUtc { get; set; }
        /// <summary>"SourceRequest" | "Candidate".</summary>
        public string RowKind { get; set; } = "Candidate";
        public string? SourceKey { get; set; }
        /// <summary>League | Federation | Broadcaster | HomeClub | AwayClub | Web.</summary>
        public string? SourceKind { get; set; }
        public string? SourceChannelOrDomain { get; set; }
        /// <summary>Kullanılan arama ifadesi: "rss:channel_id=UC…", "sitemap:https://…".</summary>
        public string? SearchExpression { get; set; }
        public int? HttpStatus { get; set; }
        public string? ErrorType { get; set; }
        public string? CandidateUrl { get; set; }
        public string? CandidateTitle { get; set; }
        public DateTime? CandidatePublishedUtc { get; set; }
        public int? DurationSeconds { get; set; }
        /// <summary>MatchHighlights | ExtendedHighlights | Goal | … (tam özet / gol klibi ayrımı).</summary>
        public string? VideoType { get; set; }
        public bool Accepted { get; set; }
        public string? VerificationStatus { get; set; }
        public string? EmbedResult { get; set; }
        public string? Evidence { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? NextAttemptUtc { get; set; }
    }
}
