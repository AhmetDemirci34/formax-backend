using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.OfficialSources
{
    /// <summary>
    /// RESMÎ KAYNAK KAYIT DEFTERİ — kilitli 11 organizasyonun her biri için ölçülmüş kaynaklar.
    ///
    /// KURAL: bir adresin burada yazılı olması onu "çalışıyor" yapmaz. <see cref="OfficialSourceStatuses.Verified"/>
    /// yalnız 11.09.2026'da CANLI erişimi ve gerçek içerik ayrıştırması ölçülmüş, parser'ı bağlı
    /// kaynaktır. Diğer durumlar (NotConfigured / Unavailable / Blocked / NeedsManualReview)
    /// ölçüm notuyla birlikte dürüstçe yazılır ve bu kaynaklara İSTEK ÜRETİLMEZ.
    ///
    /// Fetcher'ın host izin listesi YALNIZ Verified kaynakların host'larından kurulur:
    /// kayıt defterinde olmayan ya da doğrulanmamış bir host'a dış istek çıkmaz.
    ///
    /// Kullanılmayan yollar (bilerek): abonelik anahtarı isteyen uçlar (ör. LALIGA apim,
    /// Bundesliga bapi), yalnız kendi sitesine CORS veren uçlar (match.uefa.com), bot
    /// koruması arkasındaki sayfalar, Flashscore ve benzeri resmî olmayan siteler.
    /// </summary>
    public static class OfficialSourceRegistry
    {
        public const string PremierLeagueSdp = "premier-league-sdp";
        public const string SerieASdp = "seriea-sdp";
        public const string TffSite = "tff-site";
        public const string BundesligaSite = "bundesliga-site";
        public const string LaLigaSite = "laliga-site";
        public const string Ligue1Api = "ligue1-api";
        public const string EflApi = "efl-api";

        private static readonly string[] None = Array.Empty<string>();

        public static readonly IReadOnlyList<OfficialSourceDescriptor> All = new List<OfficialSourceDescriptor>
        {
            // ── 39 · Premier League ─────────────────────────────────────────────
            new(PremierLeagueSdp, "Premier League", new[] { 39 }, OfficialSourceTier.LeagueMatchCentre,
                OfficialContentKinds.OpenJson,
                new[] { "sdp-prem-prod.premier-league-prod.pulselive.com" },
                new[] { OfficialPurposes.Schedule, OfficialPurposes.Lineup, OfficialPurposes.Result,
                        OfficialPurposes.Events, OfficialPurposes.Statistics },
                OfficialSourceStatuses.Verified,
                "premierleague.com sayfası uç adresini window.SDP_API ile herkese açık yayımlıyor; " +
                "kimlik/anahtar yok. 11.09.2026: v2/matches 200 (sezon listesi), v3/matches/{id}/lineups 200, " +
                "v1/matches/{id}/events 200, v3/matches/{id}/stats 200."),

            // ── 40 · EFL Championship ───────────────────────────────────────────
            new(EflApi, "EFL Championship", new[] { 40 }, OfficialSourceTier.LeagueMatchCentre,
                OfficialContentKinds.OpenJson, new[] { "multi-club-matches.webapi.gc.eflservices.co.uk" },
                new[] { OfficialPurposes.Schedule, OfficialPurposes.Result },
                OfficialSourceStatuses.Verified,
                "15.09.2026: efl.com Nuxt paketi uç adresini MULTI_CLUB_API ile herkese açık yayımlıyor; anahtar yok, " +
                "Access-Control-Allow-Origin: *, robots.txt 403 (RFC 9309: kısıt yok). matches?competitionID=10&seasonID=2026 " +
                "200 (552 maç, matchPeriod FullTime, skor + ilk yarı skoru)."),

            // ── 140 · LALIGA ─────────────────────────────────────────────────────
            new(LaLigaSite, "LALIGA", new[] { 140 }, OfficialSourceTier.LeagueMatchCentre,
                OfficialContentKinds.HtmlEmbeddedJson, new[] { "www.laliga.com" },
                new[] { OfficialPurposes.Schedule, OfficialPurposes.Result },
                OfficialSourceStatuses.Verified,
                "15.09.2026: /laliga-easports/resultados sunucu çıktısı __NEXT_DATA__ içinde haftanın maçlarını taşıyor " +
                "(status FullTime, home_score/away_score, ISO tarih); Villarreal–Betis 1-2 okundu; robots.txt izin veriyor. " +
                "Kadro sekmesi sunucu çıktısında yok. Veri uçları (apim.laliga.com) abonelik anahtarı istiyor — KULLANILMAZ."),

            // ── 135 · Serie A ────────────────────────────────────────────────────
            new(SerieASdp, "Lega Serie A", new[] { 135 }, OfficialSourceTier.LeagueMatchCentre,
                OfficialContentKinds.OpenJson, new[] { "api-sdp.legaseriea.it" },
                new[] { OfficialPurposes.Schedule, OfficialPurposes.Lineup, OfficialPurposes.Result,
                        OfficialPurposes.Events },
                OfficialSourceStatuses.Verified,
                "legaseriea.it sayfası uç adresini SDP_API_URL ile herkese açık yayımlıyor; " +
                "Access-Control-Allow-Origin: *, anahtar yok. 11.09.2026: seasons/{s}/matches 200 (380 maç), " +
                "matches/{m}/header 200 (durum/skor/gol/kırmızı kart), matches/{m}/lineups 200 (ilk 11/yedek/diziliş/TD). " +
                "İstatistik alt kaynağı bulunamadı (404) — istatistik ÜRETİLMEZ."),

            // ── 78 · Bundesliga ──────────────────────────────────────────────────
            new(BundesligaSite, "DFL Bundesliga", new[] { 78 }, OfficialSourceTier.LeagueMatchCentre,
                OfficialContentKinds.HtmlEmbeddedJson, new[] { "www.bundesliga.com" },
                new[] { OfficialPurposes.Schedule, OfficialPurposes.Result },
                OfficialSourceStatuses.Verified,
                "13.09.2026: /de/bundesliga/spieltag herkese açık sayfası ng-state içinde haftanın maçlarını " +
                "(matchStatus, plannedKickOff, score.fulltime/halftime, takım adları) tek istekte taşıyor. " +
                "Kadro (aufstellung startingEleven) henüz bağlanmadı. Veri ucu (wapp.bapi.bundesliga.com) " +
                "anahtar istiyor — KULLANILMAZ."),

            // ── 61 · Ligue 1 ─────────────────────────────────────────────────────
            new(Ligue1Api, "Ligue 1", new[] { 61 }, OfficialSourceTier.LeagueMatchCentre,
                OfficialContentKinds.OpenJson, new[] { "ma-api.ligue1.fr" },
                new[] { OfficialPurposes.Schedule, OfficialPurposes.Result },
                OfficialSourceStatuses.Verified,
                "15.09.2026: ligue1.com Next.js paketi uç adresini L1_API_URL ile herkese açık yayımlıyor; anahtar yok, " +
                "Access-Control-Allow-Origin: *, robots.txt 404. championship-calendar/1/nearest-game-weeks ve " +
                "championship-matches/championship/1/game-week/{n} 200 (period fullTime, skor)."),

            // ── 203 · Süper Lig ──────────────────────────────────────────────────
            new(TffSite, "Türkiye Futbol Federasyonu", new[] { 203 }, OfficialSourceTier.Federation,
                OfficialContentKinds.Html, new[] { "www.tff.org" },
                new[] { OfficialPurposes.Schedule, OfficialPurposes.Lineup, OfficialPurposes.Result },
                OfficialSourceStatuses.Verified,
                "11.09.2026: pageID=198 (fikstür, 'Haftanın Maçları' tarih/saat/skor/macId) 200; " +
                "pageId=29&macId= maç sayfası 200 (İlk 11, Yedekler, Teknik Sorumlu, skor). Olay/istatistik " +
                "bu sayfada yok — ÜRETİLMEZ."),

            // ── 88 · Eredivisie ──────────────────────────────────────────────────
            new("eredivisie-site", "Eredivisie", new[] { 88 }, OfficialSourceTier.LeagueMatchCentre,
                OfficialContentKinds.Html, new[] { "eredivisie.nl" }, None,
                OfficialSourceStatuses.Blocked,
                "15.09.2026 BLOCKED (robots.txt): maç listesi yalnız /cache/site/EredivisieNL/json/fixtures.json içinde; " +
                "eredivisie.nl/robots.txt 'Disallow: /cache/'. /competitie/wedstrijd/{ev-deplasman}/ sunucu çıktısında skor yok " +
                "(yalnız data-opta-match-id). Ligin bağlantı verdiği yayıncı nos.nl sonuçları /api altından yüklüyor, " +
                "nos.nl/robots.txt 'Disallow: /api'. Engel AŞILMAZ; sonuç yazılmaz."),

            // ── 2 / 3 / 848 · UEFA ───────────────────────────────────────────────
            new("uefa-site", "UEFA", new[] { 2, 3, 848 }, OfficialSourceTier.Federation,
                OfficialContentKinds.Html, new[] { "www.uefa.com" }, None,
                OfficialSourceStatuses.NeedsManualReview,
                "11.09.2026: fikstür sayfası tarayıcıda kuruluyor; match.uefa.com CORS'u yalnız uefa.com'a " +
                "açık (üçüncü taraf kullanımına açık değil) — KULLANILMAZ. Resmî özet videosu " +
                "frame-ancestors ile gömmeye kapalı."),

            // ── Kulüp siteleri (ölçülenler) ──────────────────────────────────────
            new("venezia-site", "Venezia FC", new[] { 135 }, OfficialSourceTier.HomeClub,
                OfficialContentKinds.Sitemap, new[] { "www.veneziafc.it" }, None,
                OfficialSourceStatuses.NeedsManualReview,
                "11.09.2026: sitemap-article 200; maç günü yalnız 'convocati' haberi var, ilk 11 " +
                "yapılandırılmış veri olarak yayımlanmıyor."),
            new("fiorentina-site", "ACF Fiorentina", new[] { 135 }, OfficialSourceTier.AwayClub,
                OfficialContentKinds.Html, new[] { "www.acffiorentina.com" }, None,
                OfficialSourceStatuses.NeedsManualReview,
                "11.09.2026: ana sayfa 200; maç günü 'i convocati' haberi var, ilk 11 sayfası yok."),

            // ── Video (maç sonrası özet; mevcut zincir) ──────────────────────────
            new("trtspor", "TRT SPOR", Array.Empty<int>(), OfficialSourceTier.Broadcaster,
                OfficialContentKinds.Sitemap, new[] { "www.trtspor.com.tr" },
                new[] { OfficialPurposes.Video },
                OfficialSourceStatuses.Verified,
                "sitemap_video.xml 200 (resmî yayıncı); gömme izni oEmbed ile ayrıca doğrulanır."),
            new("youtube-official-channels", "Resmî YouTube kanalları", Array.Empty<int>(),
                OfficialSourceTier.OfficialYouTube, OfficialContentKinds.Rss,
                new[] { "www.youtube.com" }, None,
                OfficialSourceStatuses.Blocked,
                "15.09.2026 KAPALI: youtube.com/robots.txt 'Disallow: /feeds/videos.xml' (kullanıcı kararı: feed okuyucu istisnası yok). " +
                "YouTube videoları yalnız resmî sitelerin kendi yayımladığı bağlantıdan bulunur.")
        };

        /// <summary>Anahtara göre kaynak; yoksa null.</summary>
        public static OfficialSourceDescriptor? ByKey(string? key)
            => key == null ? null : All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

        /// <summary>
        /// Bir lig ve amaç için DOĞRULANMIŞ kaynaklar, öncelik sırasıyla.
        /// Doğrulanmamış kaynak asla dönmez — ona istek üretilmez.
        /// </summary>
        public static IReadOnlyList<OfficialSourceDescriptor> VerifiedFor(int leagueId, string purpose)
            => All.Where(s => s.Status == OfficialSourceStatuses.Verified
                              && s.LeagueIds.Contains(leagueId)
                              && s.Capabilities.Contains(purpose))
                  .OrderBy(s => s.Tier)
                  .ToList();

        /// <summary>Fetcher'ın host izin listesi — yalnız doğrulanmış kaynakların host'ları.</summary>
        public static IReadOnlySet<string> AllowedHosts { get; } = new HashSet<string>(
            All.Where(s => s.Status == OfficialSourceStatuses.Verified).SelectMany(s => s.Hosts),
            StringComparer.OrdinalIgnoreCase);

        /// <summary>Host izin listesinde mi? (tam eşleşme; alt alan adı serbest değildir)</summary>
        public static bool IsAllowedHost(string? host)
            => !string.IsNullOrWhiteSpace(host) && AllowedHosts.Contains(host!);

        /// <summary>Adres doğrulanmış bir resmî kaynağa mı ait? (HTTPS + izinli host + kaynak eşleşmesi)</summary>
        public static bool IsOfficialUrl(string sourceKey, string? url)
        {
            var src = ByKey(sourceKey);
            if (src == null || src.Status != OfficialSourceStatuses.Verified) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps) return false;
            return src.Hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Kilitli 11 organizasyonun lig kimlikleri — kayıt defteri her birini kapsamalı.</summary>
        public static readonly IReadOnlyList<int> LockedLeagueIds = new[] { 39, 40, 140, 135, 78, 61, 203, 88, 2, 3, 848 };
    }
}
