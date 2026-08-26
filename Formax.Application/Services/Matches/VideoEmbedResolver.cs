using System;
using System.Text.RegularExpressions;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// VİDEO OYNATMA ADRESİ — platformun KENDİ izin verdiği gömme yolu.
    ///
    /// TELİF/KULLANIM ŞARTLARI: video dosyası indirilmez, FORMAX sunucusunda barındırılmaz,
    /// proxy'lenmez. Yalnız platformun resmî embed player adresi üretilir.
    ///
    /// TANINMAYAN PLATFORM = (null, null): FORMAX içinde oynatılamaz. Bu durumda arayüz
    /// "Bu video FORMAX içinde oynatılamıyor." der ve kaynağa gitmeyi yalnız İKİNCİL
    /// seçenek olarak bırakır. ZORLA GÖMME YAPILMAZ.
    ///
    /// SAHTE KİMLİK ÜRETİLMEZ: video kimliği yalnız kaynağın GERÇEK URL'sinden ayrıştırılır;
    /// URL tanınmazsa embed yoktur.
    /// </summary>
    public static class VideoEmbedResolver
    {
        /// <summary>YouTube'un tüm yaygın bağlantı biçimleri (watch / shorts / embed / kısa).</summary>
        private static readonly Regex YouTubeId = new(
            @"(?:youtube\.com/(?:watch\?(?:[^&]*&)*v=|shorts/|embed/|live/)|youtu\.be/)([A-Za-z0-9_\-]{6,20})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public readonly record struct Result(string? EmbedUrl, string? ThumbnailUrl)
        {
            public bool Embeddable => EmbedUrl != null;
        }

        public static Result Resolve(string? platform, string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return new Result(null, null);

            // Bugün yalnız YouTube'un embed'i kullanılabiliyor. X/Instagram/TikTok için
            // resmî gömme akışı bağlanana kadar bu kaynaklar "oynatılamıyor" döner —
            // izinsiz gömme DENENMEZ.
            if (!string.Equals(platform, "YouTube", StringComparison.OrdinalIgnoreCase))
                return new Result(null, null);

            var m = YouTubeId.Match(url);
            if (!m.Success) return new Result(null, null);

            var id = m.Groups[1].Value;
            return new Result(
                $"https://www.youtube-nocookie.com/embed/{id}",
                $"https://i.ytimg.com/vi/{id}/hqdefault.jpg");
        }
    }
}
