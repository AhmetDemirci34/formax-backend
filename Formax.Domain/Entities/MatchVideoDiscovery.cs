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

        // ── RESMÎ WEB SİTESİ (15.09.2026) — YouTube RSS kapatıldı (robots Disallow); keşif siteden yapılır ──
        /// <summary>Resmî sitenin host'u ("www.legaseriea.it"); site doğrulanmadıysa null.</summary>
        public string? Domain { get; set; }
        /// <summary>"Club" | "League" | "Federation" | "Broadcaster".</summary>
        public string? SourceKind { get; set; }
        public string? Country { get; set; }
        /// <summary>Resmî sitenin kendi doğrulama durumu: "Verified" | "Candidate" | "Rejected" (kanal durumundan bağımsız).</summary>
        public string? WebsiteStatus { get; set; }
        /// <summary>Site doğrulamasının iki bağımsız kanıtı (ör. "league-link:www.laliga.com | wikidata:Q8682 P856").</summary>
        public string? WebsiteEvidence { get; set; }
        public DateTime? WebsiteVerifiedAtUtc { get; set; }
        /// <summary>"Allowed" | "PartiallyDisallowed" | "Disallowed" | "Unreachable" | null (okunmadı).</summary>
        public string? RobotsStatus { get; set; }
        public DateTime? LastSuccessUtc { get; set; }
        public string? LastError { get; set; }
        public int FailureCount { get; set; }
        /// <summary>"Closed" | "Open".</summary>
        public string CircuitState { get; set; } = "Closed";
        public DateTime? CircuitOpenUntilUtc { get; set; }
        /// <summary>Kaynak etkin mi? robots yasağı ya da kalıcı ret pasife alır; diğer kaynaklar etkilenmez.</summary>
        public bool IsActive { get; set; } = true;
        /// <summary>Resmî sitenin kendi bağlantı verdiği YouTube kanal/handle kimlikleri (virgülle) — gömülü videonun sahibini sınar.</summary>
        public string? SiteYouTubeHandles { get; set; }
        public DateTime? FeedsDiscoveredAtUtc { get; set; }
    }

    /// <summary>
    /// KALICI TARAMA İMLECİ — geçmiş maç backfill'i restart'ta baştan başlamasın diye her batch sonunda yazılır.
    /// Keyset: (LastMatchDateUtc, LastMatchId) azalan sırada; tur bitince Pass artar ve imleç sıfırlanır.
    /// </summary>
    public sealed class VideoDiscoveryCursor
    {
        public string Name { get; set; } = string.Empty;
        public DateTime? LastMatchDateUtc { get; set; }
        public int? LastMatchId { get; set; }
        public int Pass { get; set; }
        public long ScannedTotal { get; set; }
        public long EnqueuedTotal { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? PassStartedAtUtc { get; set; }
        public DateTime? LastPassCompletedAtUtc { get; set; }
    }

    /// <summary>
    /// RESMÎ SİTE AKIŞI — doğrulanmış resmî sitenin robots.txt/sayfasından otomatik bulunan makine okunur akışı
    /// (video sitemap, sitemap, YouTube dışı RSS/Atom, ana sayfa JSON-LD). Elle yazılmış liste değildir.
    /// </summary>
    public sealed class OfficialWebFeed
    {
        public int Id { get; set; }
        public string SourceKey { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        /// <summary>"VideoSitemap" | "SitemapIndex" | "Rss" | "Atom" | "HomePage".</summary>
        public string Kind { get; set; } = string.Empty;
        /// <summary>"robots:Sitemap" | "sitemap-index" | "html:link-alternate" | "homepage".</summary>
        public string DiscoveredVia { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string? RobotsStatus { get; set; }
        public DateTime? LastFetchedUtc { get; set; }
        public int? LastHttpStatus { get; set; }
        public string? LastError { get; set; }
        public int FailureCount { get; set; }
        public int LastEntryCount { get; set; }
        public DateTime NextFetchUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    /// <summary>
    /// RESMÎ SİTEDE YAYIMLANMIŞ VİDEO GİRİŞİ — akıştan okunan sayfa + (sayfa okunduysa) içindeki YouTube kimliği.
    /// Kalıcıdır: akıştan düşen giriş kaybolmaz, geçmiş maç için yeniden kullanılabilir.
    /// </summary>
    public sealed class OfficialWebVideoEntry
    {
        public long Id { get; set; }
        public string SourceKey { get; set; } = string.Empty;
        public int? FeedId { get; set; }
        public string PageUrl { get; set; } = string.Empty;
        /// <summary>SHA-256(PageUrl) — tekillik anahtarı (URL 600 karakteri aşabilir).</summary>
        public string PageUrlHash { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        /// <summary>Aksansız, küçük harf başlık — SQL'de Turkish_CI_AS tuzağına düşmeden arama için.</summary>
        public string FoldedTitle { get; set; } = string.Empty;
        public DateTime? PublishedUtc { get; set; }
        /// <summary>"Exact" | "Day" | "SeenOnly".</summary>
        public string DatePrecision { get; set; } = "Exact";
        /// <summary>Sayfanın gömdüğü resmî YouTube videosu (tek ve eşleştirilebilir ise).</summary>
        public string? YouTubeVideoId { get; set; }
        /// <summary>"JsonLdEmbedUrl" | "Iframe" | "WatchLink" | "Sitemap" | null.</summary>
        public string? VideoIdEvidence { get; set; }
        public string? JsonLdName { get; set; }
        public DateTime? JsonLdUploadUtc { get; set; }
        public DateTime? PageFetchedAtUtc { get; set; }
        public int? PageHttpStatus { get; set; }
        /// <summary>"Ok" | "NoVideo" | "Ambiguous" | "RobotsDisallowed" | "Error".</summary>
        public string? PageOutcome { get; set; }
        public DateTime FirstSeenUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
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
        /// <summary>"Today" | "Yesterday" | "Last7Days" | "Last30Days" | "CurrentSeason" | "OlderSeason" | "Backfill" (eski).</summary>
        public string EnqueueReason { get; set; } = "Backfill";
        /// <summary>Son yeniden kuyruğa alma nedeni: "Revalidation:{kod}" | "PlayerError:{kod}" | "OfficialResult".</summary>
        public string? RequeueReason { get; set; }
        public DateTime? RequeuedAtUtc { get; set; }
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
