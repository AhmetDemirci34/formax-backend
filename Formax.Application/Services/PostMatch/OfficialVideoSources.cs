using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.PostMatch
{
    /// <summary>
    /// RESMÎ VİDEO KAYNAĞI — izin listesindeki TEK kayıt tipi.
    ///
    /// <paramref name="AllowsInAppEmbed"/> kaynağın BİLİNEN politikasıdır, garanti değil:
    /// video başına embed doğrulaması yine yapılır. false ise doğrulama hiç denenmez —
    /// engeli bilinen bir kaynağı sürekli yoklamak hem boşuna istektir hem de "belki bu
    /// sefer açılır" diye engel dolanmaya çalışmaktır.
    /// </summary>
    public sealed record OfficialVideoSource(
        string Key,
        string Publisher,
        string Platform,
        string? YouTubeChannelId,
        bool AllowsInAppEmbed,
        string Why,
        /// <summary>Hak sahipligi kademesi — bkz. <see cref="OfficialVideoSourceTiers"/>.</summary>
        int Tier = OfficialVideoSourceTiers.LicensedSportsOutlet,
        /// <summary>Kulup kaynaklarinda kulubun adi; ev/deplasman kademesi bununla cozulur.</summary>
        string? ClubName = null);

    /// <summary>
    /// RESMÎ KAYNAK İZİN LİSTESİ — bir videonun "resmî" sayılmasının TEK ölçütü.
    ///
    /// NEDEN KANAL KİMLİĞİ, NEDEN BAŞLIK DEĞİL: "Fenerbahçe - Lyon MAÇ ÖZETİ" başlıklı
    /// yüzlerce yükleme var ve bunların ezici çoğunluğu lisanssız yeniden yüklemedir.
    /// Başlık, kanal adı ve hatta "official" kelimesi taklit edilebilir; kanal kimliği
    /// (UC…) edilemez. Bu yüzden eşleşme YALNIZ kimlik üzerinden yapılır.
    ///
    /// Kimlikler 02.09.2026'da kanalların kendi sayfalarındaki canonical bağlantıdan
    /// okunmuştur.
    /// </summary>
    public static class OfficialVideoSources
    {
        /// <summary>UEFA'nın kendi sitesi — maçın hak sahibi, ekranda ANA otorite.</summary>
        public const string UefaWeb = "uefa.com";

        public static readonly IReadOnlyList<OfficialVideoSource> All = new[]
        {
            // Öncelik 1 — UEFA. Resmî özet VARDIR ama uygulama içinde OYNATILAMAZ:
            // uefa.com "frame-ancestors 'self' https://idp-production.uefa.com" gönderir
            // (ölçüldü 02.09.2026, tarayıcı konsolu). Bu bir teknik kaza değil, yayıncının
            // kararıdır ve AŞILMAZ; kayıt EmbedBlocked olarak saklanır.
            new OfficialVideoSource(UefaWeb, "UEFA", "Web", null, false,
                "uefa.com CSP frame-ancestors 'self' — gomme reddedilir.",
                OfficialVideoSourceTiers.Federation),

            new OfficialVideoSource("uefa-youtube", "UEFA", "YouTube",
                "UCyGa1YEx9ST66rYrJTGIKOw", true,
                "UEFA resmi YouTube kanali.",
                OfficialVideoSourceTiers.Federation),

            // Öncelik 2-3 — kulüpler. UEFA maçlarında kulüplerin maç görüntüsü hakkı
            // yoktur; kanallarında basın toplantısı/kamera arkası bulunur. İzin listesinde
            // dururlar ama tür süzgeci (MatchVideoTypes) o içerikleri zaten eler.
            new OfficialVideoSource("fenerbahce", "Fenerbahçe SK", "YouTube",
                "UCgqlho3-8a6FmDqQm7Q6gJw", true,
                "Fenerbahce SK resmi YouTube kanali.",
                OfficialVideoSourceTiers.Club, "Fenerbahçe"),

            new OfficialVideoSource("olympique-lyonnais", "Olympique Lyonnais", "YouTube",
                "UCzHCZXmqIdjqRnpdp0l_T6g", true,
                "Olympique Lyonnais resmi YouTube kanali.",
                OfficialVideoSourceTiers.Club, "Olympique Lyonnais"),

            // Öncelik 4 — maçın resmî yayıncısı. Türkiye yayın hakkı TRT'dedir ve resmî
            // özet TRT SPOR kanalında yayımlanır.
            new OfficialVideoSource("trt-spor", "TRT SPOR", "YouTube",
                "UCfYNqluOf8EbQkL44otydMw", true,
                "TRT SPOR — macin Turkiye resmi yayincisinin kanali.",
                OfficialVideoSourceTiers.Broadcaster),
        };

        /// <summary>YouTube kanal kimliğinden resmî kaynak; listede yoksa null.</summary>
        public static OfficialVideoSource? ByYouTubeChannel(string? channelId)
            => string.IsNullOrWhiteSpace(channelId)
                ? null
                : All.FirstOrDefault(s => string.Equals(s.YouTubeChannelId, channelId, StringComparison.Ordinal));

        /// <summary>Anahtardan resmî kaynak; listede yoksa null.</summary>
        public static OfficialVideoSource? ByKey(string? key)
            => string.IsNullOrWhiteSpace(key)
                ? null
                : All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// KEŞİF SIRASI — kademeye göre. Maç bağlamı verilirse ev sahibi kulüp
        /// deplasman kulübünün ÖNÜNE geçer; verilmezse liste sırası korunur.
        ///
        /// Sıra bir tercih değil, HAK SAHİPLİĞİ düzenidir: aynı maçın özetini hem
        /// federasyon hem kulüp yayımladığında federasyonunki ana kayıttır.
        /// </summary>
        public static IReadOnlyList<OfficialVideoSource> DiscoverableYouTubeChannels(
            string? homeTeamName = null, string? awayTeamName = null)
            => All.Where(s => s.Platform == "YouTube"
                           && s.AllowsInAppEmbed
                           && !string.IsNullOrWhiteSpace(s.YouTubeChannelId))
                  .OrderBy(s => EffectiveTier(s, homeTeamName, awayTeamName))
                  .ThenBy(s => s.Key, StringComparer.Ordinal)
                  .ToList();

        /// <summary>
        /// MAÇ BAĞLAMINDAKİ KADEME. Kulüp kaynağı, maçtaki konumuna göre 4 (ev) ya da
        /// 5 (deplasman) olur; iki takımın da adı eşleşmiyorsa kaynak bu maç için
        /// kulüp otoritesi TAŞIMAZ ve lisanslı kaynak kademesine düşer.
        /// </summary>
        public static int EffectiveTier(
            OfficialVideoSource source, string? homeTeamName, string? awayTeamName)
        {
            if (source is null) return OfficialVideoSourceTiers.AuxiliaryDiscovery;
            if (source.Tier != OfficialVideoSourceTiers.Club) return source.Tier;

            var club = source.ClubName;
            if (string.IsNullOrWhiteSpace(club)) return OfficialVideoSourceTiers.LicensedSportsOutlet;

            if (NameMatches(club, homeTeamName)) return OfficialVideoSourceTiers.Club;
            if (NameMatches(club, awayTeamName)) return OfficialVideoSourceTiers.AwayClub;

            // Bu maçın tarafı değil — kulüp kanalı olması ona bu maçta öncelik vermez.
            return OfficialVideoSourceTiers.LicensedSportsOutlet;
        }

        /// <summary>Kulüp adı eşleşmesi — biri diğerini içeriyorsa yeterli ("Fenerbahçe" / "Fenerbahçe SK").</summary>
        private static bool NameMatches(string club, string? teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName)) return false;
            var a = Formax.Application.Services.News.Intelligence.NewsTextNormalizer.Fold(club);
            var b = Formax.Application.Services.News.Intelligence.NewsTextNormalizer.Fold(teamName);
            if (a.Length == 0 || b.Length == 0) return false;
            return a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);
        }
    }
}
