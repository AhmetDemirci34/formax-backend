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
        string Reason);

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
    /// RESMÎ VİDEO KEŞFİ — bitmiş bir maç için resmî kaynaklarda aday arar.
    ///
    /// SÖZLEŞME: uygulama, bir istek başına SINIRLI sayıda dış çağrı yapar ve
    /// api-football kotasına DOKUNMAZ. Arama motoru döngüsü, scraping ve sayfa
    /// tıklamasına bağlı istek YASAKTIR.
    /// </summary>
    public interface IOfficialMatchVideoProvider
    {
        string Name { get; }

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
