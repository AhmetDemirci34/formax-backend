using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.PostMatch;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// EMBED DOĞRULAMA SONUCU.
    ///
    /// <paramref name="Embeddable"/> false ise bu bir HATA DEĞİL, yayıncının kararıdır.
    /// Kayıt yine saklanır (kullanıcı kaynağa gidebilsin) ama uygulama içinde oynatılmaz.
    /// </summary>
    public sealed record EmbedVerification(
        bool Embeddable,
        string? EmbedUrl,
        string? ThumbnailUrl,
        string Reason,
        /// <summary>oEmbed başlığı (tarihsiz resmî bağlantıda başlık kanıtı).</summary>
        string? Title = null,
        string? AuthorName = null,
        string? AuthorUrl = null,
        /// <summary>true = video kaldırılmış/gizli/erişilemez (oEmbed 400/404) — SourceBlocked gerekçesi.</summary>
        bool Unavailable = false);

    /// <summary>
    /// BİR VİDEONUN UYGULAMA İÇİNDE OYNATILABİLİRLİĞİNİ DOĞRULAR.
    ///
    /// Doğrulama YALNIZ kaynağın kendi resmî ucundan yapılır (YouTube için oEmbed).
    /// DRM/CSP/X-Frame-Options/referer engelini dolanmak bu arayüzün işi DEĞİLDİR ve
    /// hiçbir uygulaması bunu yapmaz: engel varsa cevap "oynatılamaz"dır.
    /// </summary>
    public interface IVideoEmbedVerifier
    {
        Task<EmbedVerification> VerifyAsync(
            OfficialVideoCandidate candidate, OfficialVideoSource source, CancellationToken ct = default);
    }

    /// <summary>
    /// BİR KEŞİF SAĞLAYICISININ DURUMU.
    ///
    /// <see cref="NotConfigured"/> bir HATA DEĞİLDİR: sağlayıcının çalışması için gereken
    /// resmî erişim (ör. API anahtarı, yayıncı uç adresi) bu kurulumda verilmemiştir.
    /// Bu durumda sağlayıcı SESSİZCE ATLANIR ve zincir yapılandırılmış olanlarla devam
    /// eder. Anahtar üretmek, gizli uç aramak veya kısıtı dolanmak YASAKTIR.
    /// </summary>
    public static class VideoProviderStatuses
    {
        /// <summary>Çalışmaya hazır — gereken erişim tanımlı.</summary>
        public const string Configured = "Configured";

        /// <summary>Gerekli resmî erişim verilmemiş; sağlayıcı atlanır, zincir devam eder.</summary>
        public const string NotConfigured = "NotConfigured";

        /// <summary>Yapılandırmayla kapatılmış.</summary>
        public const string Disabled = "Disabled";
    }

    /// <summary>
    /// KAYNAK ERİŞİLEMEDİ — plan/rate limit engeli, HTTP hatası ya da ağ hatası.
    ///
    /// Bu bir "arandı ve bulunamadı" sonucu DEĞİLDİR. Sağlayıcı bu istisnayı atar; zincir
    /// onu yakalar ve turu "tamamlanmadı" olarak işaretler. İş de bu turu kalıcı defterde
    /// deneme SAYMAZ — aksi hâlde bir rate limit dalgası kullanıcıyı erken
    /// "bulunamadı"ya götürürdü.
    /// </summary>
    public sealed class VideoProviderUnavailableException : Exception
    {
        public VideoProviderUnavailableException(string provider, string reason, bool rateLimited = false)
            : base(provider + ": " + reason)
        {
            Provider = provider; Reason = reason; RateLimited = rateLimited;
        }

        public string Provider { get; }
        public string Reason { get; }
        /// <summary>429 / kota / plan engeli.</summary>
        public bool RateLimited { get; }
    }

    /// <summary>
    /// ZİNCİRİN SON TURU — iş, turu defterde sayıp saymayacağına buradan karar verir.
    /// </summary>
    public interface IVideoDiscoveryDiagnostics
    {
        IReadOnlyList<VideoProviderOutcome> LastOutcomes { get; }

        /// <summary>
        /// En az bir yapılandırılmış sağlayıcı aramasını HATASIZ tamamladı mı? false ise
        /// tur engellenmiştir ve deneme sayılmaz.
        /// </summary>
        bool LastRunCompleted { get; }
    }

    /// <summary>Tek bir sağlayıcının bir maç için ne yaptığı — teşhis ve rapor kaydı.</summary>
    public sealed record VideoProviderOutcome(
        string Provider,
        string Status,
        int Priority,
        int CandidateCount,
        string Note);

    /// <summary>
    /// RESMÎ VİDEO KEŞFİ — bitmiş bir maç için resmî kaynaklarda aday arar.
    ///
    /// SÖZLEŞME: uygulama, bir istek başına SINIRLI sayıda dış çağrı yapar ve
    /// api-football kotasına DOKUNMAZ. Arama motoru döngüsü, scraping ve sayfa
    /// tıklamasına bağlı istek YASAKTIR.
    /// </summary>
    public interface IOfficialMatchVideoProvider
    {
        string Name { get; }
        /// <summary>
        /// KEŞİF SIRASI — küçük sayı önce çalışır
        /// (<see cref="Formax.Application.Services.PostMatch.OfficialVideoSourceTiers"/>).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// <see cref="VideoProviderStatuses"/>. <c>NotConfigured</c> sağlayıcı hiç
        /// çağrılmaz; zincir bir sonrakiyle devam eder.
        /// </summary>
        string Status { get; }


        Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(
            VideoFixtureIdentity fixture, CancellationToken ct = default);
    }

    /// <summary>Bir adayın kayıt denemesinin sonucu.</summary>
    public sealed record MatchVideoRegistration(
        bool Stored,
        string Status,
        string Reason,
        long? VideoId = null);

    /// <summary>
    /// VİDEO YAZMA KAPISI — MatchVideos tablosuna yazan TEK yol.
    ///
    /// Kimlik doğrulaması, embed doğrulaması ve tekilleştirme burada yapılır; başka
    /// hiçbir yer bu tabloya doğrudan yazmaz. Kural tek yerde olmazsa her çağıran kendi
    /// gevşek sürümünü yazar ve yanlış ayağın videosu er geç sızar.
    /// </summary>
    public interface IMatchVideoRegistrar
    {
        Task<MatchVideoRegistration> RegisterAsync(
            int matchId, OfficialVideoCandidate candidate, CancellationToken ct = default);

        /// <summary>Maçın kimlik kanıtlarını (fikstür kimliği, yön, diğer ayaklar) kurar.</summary>
        Task<VideoFixtureIdentity?> BuildIdentityAsync(int matchId, CancellationToken ct = default);
    }
}
