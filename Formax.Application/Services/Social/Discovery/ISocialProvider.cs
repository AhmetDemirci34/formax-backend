using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Social.Discovery
{
    /// <summary>
    /// Phase 7 — platform-genişletilebilir resmi sosyal medya sağlayıcısı. Her platform
    /// (YouTube/X/Instagram/Facebook/RSS) bu arayüzü uygular; yenisini eklemek = yeni sınıf
    /// (yeni mimari kurulmaz). Kimlik bilgisi yoksa <see cref="IsEnabled"/>=false → boş döner
    /// (fake ÜRETMEZ). Yalnız verified resmi hesabı okur.
    /// </summary>
    public interface ISocialProvider
    {
        /// <summary>"YouTube" | "X" | "Instagram" | "Facebook" | "RSS".</summary>
        string Platform { get; }

        /// <summary>Kimlik/erişim yapılandırıldıysa true; değilse false (boş sonuç, fake yok).</summary>
        bool IsEnabled { get; }

        /// <summary>Bu sağlayıcı hesabı okuyabiliyor mu (platform eşleşmesi).</summary>
        bool CanHandle(OfficialSocialAccount account);

        /// <summary>Hesabın son paylaşımlarını (yalnız gerçek) getirir. Hata/erişimsizlik → boş.</summary>
        Task<IReadOnlyList<SocialCandidate>> FetchAsync(OfficialSocialAccount account, CancellationToken ct = default);
    }
}
