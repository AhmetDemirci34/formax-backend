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
        string? ClubName = null,
        /// <summary>
        /// Lig/yayinci kaynaginin YAYIN KAPSAMI (kanonik LeagueId). null = kapsam sinirsiz (UEFA, TRT gibi
        /// mevcut kayitlar). Kapsam disi maçta kanal HIC okunmaz: ilgisiz akislar dis istek harcamasin.
        /// </summary>
        IReadOnlyList<int>? LeagueIds = null,
        /// <summary>Kulüp kaynağının kanonik takım kimliği (katalogdan). Doluysa maç tarafı kimlikle çözülür.</summary>
        int? TeamId = null);

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

        /// <summary>TRT SPOR resmî sitesi — maçın Türkiye resmî yayıncısı.</summary>
        public const string TrtSporWeb = "trtspor.com.tr";

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

            // YAYIN HAKKI SAHİBİ TOHUMU. Lig ve kulüp kanalları artık BURADA YAZILMAZ: resmî kaynak kataloğu
            // (OfficialVideoSourceCatalog) Wikidata + kulübün kendi sitesi kanıtıyla otomatik keşfeder.
            // Türkiye resmî yayıncısı — akışında ölçülen kapsam: Süper Lig ve Ligue 1 özetleri.
            new OfficialVideoSource("bein-sports-turkiye", "beIN SPORTS Türkiye", "YouTube",
                "UCPe9vNjHF1kEExT5kHwc7aw", true,
                "beIN SPORTS Turkiye resmi YouTube kanali.",
                OfficialVideoSourceTiers.Broadcaster, LeagueIds: new[] { 203, 61 }),

            // TRT SPOR resmî sitesi — video sitemap'i yayımlar (sitemap_video.xml).
            // GÖMMEYE KAPALI: video sayfaları "X-Frame-Options: SAMEORIGIN" gönderir
            // (ölçüldü 11.09.2026). HLS akışını doğrudan oynatmak yayıncının oynatıcısını
            // ve kısıtlarını dolanmak olur — YAPILMAZ. Kayıt en fazla "resmî kaynakta izle".
            new OfficialVideoSource(TrtSporWeb, "TRT SPOR", "Web", null, false,
                "trtspor.com.tr X-Frame-Options SAMEORIGIN — gomme reddedilir.",
                OfficialVideoSourceTiers.Broadcaster),
        };

        /// <summary>YouTube kanal kimliğinden resmî kaynak; listede yoksa null.</summary>
        public static OfficialVideoSource? ByYouTubeChannel(string? channelId, IReadOnlyList<OfficialVideoSource>? sources = null)
            => string.IsNullOrWhiteSpace(channelId)
                ? null
                : (sources ?? All).FirstOrDefault(s => string.Equals(s.YouTubeChannelId, channelId, StringComparison.Ordinal));

        /// <summary>Anahtardan resmî kaynak; listede yoksa null.</summary>
        public static OfficialVideoSource? ByKey(string? key, IReadOnlyList<OfficialVideoSource>? sources = null)
            => string.IsNullOrWhiteSpace(key)
                ? null
                : (sources ?? All).FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// KEŞİF SIRASI — kademeye göre. Maç bağlamı verilirse ev sahibi kulüp
        /// deplasman kulübünün ÖNÜNE geçer; verilmezse liste sırası korunur.
        ///
        /// Sıra bir tercih değil, HAK SAHİPLİĞİ düzenidir: aynı maçın özetini hem
        /// federasyon hem kulüp yayımladığında federasyonunki ana kayıttır.
        /// </summary>
        public static IReadOnlyList<OfficialVideoSource> DiscoverableYouTubeChannels(
            string? homeTeamName = null, string? awayTeamName = null, int? leagueId = null,
            IReadOnlyList<OfficialVideoSource>? sources = null, int? homeTeamId = null, int? awayTeamId = null)
            => (sources ?? All).Where(s => s.Platform == "YouTube"
                           && s.AllowsInAppEmbed
                           && !string.IsNullOrWhiteSpace(s.YouTubeChannelId)
                           && IsRelevant(s, homeTeamName, awayTeamName, leagueId, homeTeamId, awayTeamId))
                  .OrderBy(s => EffectiveTier(s, homeTeamName, awayTeamName))
                  .ThenBy(s => s.Key, StringComparer.Ordinal)
                  .ToList();

        /// <summary>
        /// Kaynak BU maç için okunmalı mı? Kulüp kanalı yalnız kendi maçında; kapsamı tanımlı lig/yayıncı
        /// kanalı yalnız kapsamındaki ligde. Maç bağlamı verilmezse (teşhis/test) eleme yapılmaz.
        /// </summary>
        public static bool IsRelevant(OfficialVideoSource source, string? homeTeamName, string? awayTeamName, int? leagueId,
            int? homeTeamId = null, int? awayTeamId = null)
        {
            if (source.LeagueIds is { Count: > 0 } leagues && leagueId is int l && !leagues.Contains(l)) return false;
            // Katalog kulübü: takım KİMLİĞİ ile (ad benzerliği değil).
            if (source.TeamId is int tid && (homeTeamId != null || awayTeamId != null))
                return tid == homeTeamId || tid == awayTeamId;
            if (source.Tier == OfficialVideoSourceTiers.Club && !string.IsNullOrWhiteSpace(source.ClubName)
                && (homeTeamName != null || awayTeamName != null))
                return NameMatches(source.ClubName!, homeTeamName) || NameMatches(source.ClubName!, awayTeamName);
            return true;
        }

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
            if (source.TeamId is int && club != null && (NameMatches(club, homeTeamName) || NameMatches(club, awayTeamName)))
                return NameMatches(club, homeTeamName) ? OfficialVideoSourceTiers.Club : OfficialVideoSourceTiers.AwayClub;
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
