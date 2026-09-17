using System;
using System.Collections.Generic;

namespace Formax.Application.Services.OfficialSources
{
    /// <summary>Resmî kaynaktan okunan verinin kullanım amacı — defterde ve kayıt defterinde aynı.</summary>
    public static class OfficialPurposes
    {
        public const string Schedule = "Schedule";
        public const string Lineup = "Lineup";
        public const string Result = "Result";
        public const string Events = "Events";
        public const string Statistics = "Statistics";
        public const string Critical = "Critical";
        public const string Video = "Video";
    }

    /// <summary>
    /// KAYNAK DURUMU — ölçülmüş gerçek. Boş sağlayıcı sınıfı <see cref="Verified"/> olamaz:
    /// Verified yalnız canlı erişimi ve gerçek içerik ayrıştırması kanıtlanmış kaynaktır.
    /// </summary>
    public static class OfficialSourceStatuses
    {
        /// <summary>Canlı erişim + gerçek içerik ayrıştırma kanıtlandı; parser bağlı.</summary>
        public const string Verified = "Verified";
        /// <summary>Bu organizasyon için yapılandırılmış/uygulanmış kaynak yok.</summary>
        public const string NotConfigured = "NotConfigured";
        /// <summary>Kaynak erişilemiyor (bağlantı yok / 5xx / içerik yok).</summary>
        public const string Unavailable = "Unavailable";
        /// <summary>Bot koruması / CAPTCHA / erişim engeli — AŞILMAZ.</summary>
        public const string Blocked = "Blocked";
        /// <summary>Kaynak var ama veri yalnız tarayıcıda kurulan ya da kısıtlı bir uçta; elle inceleme gerekir.</summary>
        public const string NeedsManualReview = "NeedsManualReview";
        /// <summary>
        /// Kullanılabilir yasal/resmî sonuç kaynağı YOK (robots yasağı, yalnız istemci tarafı veri, kulüp sitelerinde yapılandırılmış
        /// sonuç yok). Bu organizasyonun sonucu yazılamaz; teşhiste ResultSourceUnavailable olarak görünür.
        /// </summary>
        public const string Unsupported = "Unsupported";
    }

    /// <summary>Kaynak önceliği — küçük değer önce denenir.</summary>
    public enum OfficialSourceTier
    {
        Federation = 1,
        LeagueMatchCentre = 2,
        HomeClub = 3,
        AwayClub = 4,
        Broadcaster = 5,
        OfficialYouTube = 6,
        OfficialSocial = 7,
        LicensedSports = 8
    }

    /// <summary>Kaynağın içerik biçimi.</summary>
    public static class OfficialContentKinds
    {
        public const string OpenJson = "OpenJson";
        public const string Html = "Html";
        public const string HtmlEmbeddedJson = "HtmlEmbeddedJson";
        public const string JsonLd = "JsonLd";
        public const string Rss = "Rss";
        public const string Sitemap = "Sitemap";
        public const string OEmbed = "oEmbed";
    }

    /// <summary>Kayıt defterindeki tek resmî kaynak.</summary>
    public sealed record OfficialSourceDescriptor(
        string Key,
        string Organization,
        IReadOnlyList<int> LeagueIds,
        OfficialSourceTier Tier,
        string Kind,
        IReadOnlyList<string> Hosts,
        IReadOnlyList<string> Capabilities,
        string Status,
        string EvidenceNote);

    /// <summary>Kaynağın bildirdiği maç durumu — FORMAX Match.Status sözlüğüne çevrilmeden önceki hali.</summary>
    public static class OfficialMatchStatuses
    {
        public const string Scheduled = "Scheduled";
        public const string Live = "Live";
        public const string Finished = "Finished";
        /// <summary>Uzatmalarda bitti (kaynak açıkça yayımladıysa).</summary>
        public const string FinishedAfterExtraTime = "FinishedAET";
        /// <summary>Seri penaltılarla bitti (kaynak açıkça yayımladıysa).</summary>
        public const string FinishedAfterPenalties = "FinishedPEN";
        /// <summary>Yarıda kaldı.</summary>
        public const string Abandoned = "Abandoned";
        public const string Postponed = "Postponed";
        public const string Cancelled = "Cancelled";
        public const string Suspended = "Suspended";
        public const string Unknown = "Unknown";
    }

    /// <summary>Resmî kaynaktaki tek maç satırı (kimlik + durum + skor).</summary>
    public sealed record OfficialMatchRecord(
        string SourceKey,
        string OfficialMatchId,
        string? OfficialUrl,
        string HomeName,
        string AwayName,
        DateTime? KickoffUtc,
        string Status,
        int? HomeScore,
        int? AwayScore,
        string? RawStatus,
        string? Venue = null,
        int? HalfTimeHome = null,
        int? HalfTimeAway = null,
        IReadOnlyDictionary<string, string>? Extra = null);

    public sealed record OfficialLineupPlayer(
        string Name,
        int? ShirtNumber,
        string? Position,
        bool IsCaptain,
        string? OfficialPlayerId = null,
        // Saha konumu "hat:sıra" — YALNIZ kaynağın kendi hat/koordinat verisinden; yoksa null.
        string? Grid = null);

    /// <summary>Tek takımın resmî kadrosu.</summary>
    public sealed record OfficialLineupSide(
        string TeamName,
        string? Formation,
        IReadOnlyList<OfficialLineupPlayer> Starters,
        IReadOnlyList<OfficialLineupPlayer> Bench,
        string? Coach);

    /// <summary>Resmî kaynaktan okunan kadro belgesi — taraflardan biri null olabilir.</summary>
    public sealed record OfficialLineupDocument(
        string SourceKey,
        string OfficialMatchId,
        string SourceUrl,
        string ContentHash,
        DateTime? PublishedAtUtc,
        OfficialLineupSide? Home,
        OfficialLineupSide? Away);

    /// <summary>Tur bağlamı — aynı tur aynı adresi bir kez indirir.</summary>
    public sealed record OfficialRoundContext(string RoundKey, DateTime UtcNow, string Purpose, int? MatchId = null);

    /// <summary>Bir sağlayıcı okumasının sonucu: değer + ölçülmüş sonuç.</summary>
    public sealed record OfficialRead<T>(T? Value, string Outcome, string? Detail, OfficialFetchResult? Fetch)
    {
        public bool Ok => Outcome == OfficialReadOutcomes.Ok;
    }

    public static class OfficialReadOutcomes
    {
        /// <summary>Kaynak geçerli cevap verdi (değer boş olabilir: "henüz yayımlanmadı").</summary>
        public const string Ok = "Ok";
        /// <summary>İstek güvenlik/ağ/HTTP nedeniyle tamamlanamadı — kontrol SAYILMAZ.</summary>
        public const string FetchFailed = "FetchFailed";
        /// <summary>Cevap geldi ama beklenen biçimde değil — kontrol SAYILMAZ.</summary>
        public const string ParseFailed = "ParseFailed";
        /// <summary>Kaynak bu amacı desteklemiyor.</summary>
        public const string NotSupported = "NotSupported";
    }

    /// <summary>Tek resmî web isteği.</summary>
    public sealed record OfficialFetchRequest(
        string SourceKey,
        string Provider,
        string Url,
        string Purpose,
        string? RoundKey,
        int? MatchId = null,
        string Accept = "*/*",
        string? Encoding = null);

    /// <summary>
    /// Resmî web isteğinin sonucu. <see cref="Body"/> yalnız başarılı (200/304) okumada doludur.
    /// </summary>
    public sealed record OfficialFetchResult(
        string Url,
        string Outcome,
        int? HttpStatus,
        string? Body,
        string? ContentHash,
        bool FromCache,
        bool ContentChanged,
        bool AlreadyProcessed,
        long LedgerId)
    {
        public bool Ok => Body != null;
    }

    /// <summary>Fetcher sonuç kodları (defterdeki <c>Outcome</c>).</summary>
    public static class OfficialFetchOutcomes
    {
        public const string Fetched = "Fetched";
        public const string NotModified = "NotModified";
        public const string RoundMemo = "RoundMemo";
        public const string HostNotAllowed = "HostNotAllowed";
        public const string NotHttps = "NotHttps";
        public const string PrivateAddress = "PrivateAddress";
        public const string RedirectRejected = "RedirectRejected";
        public const string TooLarge = "TooLarge";
        public const string Timeout = "Timeout";
        public const string HttpError = "HttpError";
        public const string NetworkError = "NetworkError";
        public const string RateLimited = "RateLimited";
        /// <summary>robots.txt bu yolu yasaklıyor ya da robots.txt ulaşılamaz (RFC 9309) — istek yapılmadı.</summary>
        public const string RobotsDisallowed = "RobotsDisallowed";
    }
}
