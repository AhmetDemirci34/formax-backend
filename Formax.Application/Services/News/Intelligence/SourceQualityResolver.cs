using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.News.Intelligence
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Source Quality. Her kaynağa (yayıncı/domain) güvenilirlik
    /// puanı verir; Confidence hesabında ve Evidence kalite kapısında kullanılır.
    /// Resmi kulüp/federasyon en yüksek, doğrulanmamış sosyal içerik en düşük.
    ///
    /// MODEL KORUNDU (tier'lar ve puanlar aynı). DÜZELTİLEN: gerçek futbol yayıncılarının
    /// (özellikle TÜRKİYE ve resmi kulüp domainlerinin) whitelist'te olmadıkları için
    /// "bilinmeyen" (60) sayılıp kalite kapısında (85) elenmesi. Ölçüldü: Türkçe kaynaklı
    /// gerçek sakatlık/kadro haberleri tam olarak bu yüzden AI'a hiç ulaşmıyordu.
    /// </summary>
    public sealed class SourceQualityResolver
    {
        // ── TIER 1 (95) — Federasyon / lig / resmi kulüp ────────────────────────────────
        private static readonly string[] Federation =
        {
            "fifa.com", "uefa.com", "uefa.org", "fifa", "uefa", "conmebol", "concacaf",
            "thefa.com", "the fa", "federation", "federasyon", "tff.org", "tff",
            "turkiye futbol federasyonu"
        };

        private static readonly string[] LeagueOfficial =
        {
            "premierleague.com", "laliga.com", "bundesliga.com", "legaseriea.it",
            "seriea.com", "ligue1.com", "ligue1.fr", "eredivisie.nl", "efl.com",
            "trendyolsuperlig", "superlig.com", "mls", "primeraliga"
        };

        private static readonly string[] OfficialClub =
        {
            "official", "club statement", "resmi aciklama",
            "arsenal.com", "liverpoolfc", "realmadrid", "fcbarcelona", "mancity.com",
            "manutd.com", "chelseafc.com", "tottenhamhotspur", "nufc.co.uk", "avfc.co.uk",
            "fcbayern", "bvb.de", "juventus.com", "acmilan.com", "inter.it", "asroma.com",
            "sscnapoli", "atleticodemadrid", "sevillafc", "psg.fr", "ocl.olympiquelyonnais",
            "ajax.nl", "psv.nl", "feyenoord.nl", "benfica.pt", "slbenfica", "fcporto.pt",
            "sporting.pt", "galatasaray.org", "fenerbahce.org", "bjk.com.tr",
            "trabzonspor.org.tr", "basaksehir.com.tr", "goztepe.com.tr"
        };

        // ── TIER 2 (90) — Uluslararası büyük medya / ajans ─────────────────────────────
        private static readonly string[] MajorMedia =
        {
            "bbc", "espn", "sky sports", "skysports", "the guardian", "guardian",
            "goal.com", "goal ", "reuters", "the athletic", "nytimes.com/athletic",
            "athletic", "ap news", "apnews", "associated press", "afp", "bloomberg",
            "cnn", "dw.com", "telegraph", "the times", "thetimes.co.uk", "independent.co.uk"
        };

        // ── TIER 2b (85) — Doğrulanmış spor yayıncıları (uluslararası) ─────────────────
        private static readonly string[] VerifiedSports =
        {
            "marca", "as.com", "diarioas", "mundodeportivo", "sport.es", "relevo",
            "fabrizio", "romano", "gazzetta", "corrieredellosport", "tuttosport",
            "kicker", "bild.de/sport", "sportbild", "lequipe", "rmcsport", "footmercato",
            "football italia", "evening standard", "standard.co.uk", "mirror", "the sun",
            "football.london", "manchestereveningnews", "liverpoolecho", "birminghammail",
            "sportsmole", "90min", "onefootball", "flashscore", "besoccer", "transfermarkt",
            "record.pt", "abola", "ojogo", "publico.pt", "voetbalprimeur", "vi.nl",
            "sportbladet", "sportskeeda", "espnfc"
        };

        // ── TIER 2c (85) — TÜRKİYE doğrulanmış ulusal spor/haber yayıncıları ───────────
        // Bunlar kilitli liglerin (Süper Lig + Avrupa kupalarındaki Türk takımları) BİRİNCİL
        // haber kaynağıdır. Whitelist dışı kalınca gerçek kadro/sakatlık haberi eleniyordu.
        private static readonly string[] TurkishNational =
        {
            "beinsports.com.tr", "bein sports", "ntvspor.net", "ntvspor", "ntv.com.tr",
            "ntv ", "trtspor", "trt spor", "trthaber", "aa.com.tr", "anadolu ajansi",
            "anadolu agency", "fotomac.com.tr", "fotomac", "fanatik.com.tr", "fanatik",
            "ajansspor.com", "ajansspor", "sporx.com", "sporx", "hurriyet.com.tr",
            "hurriyet", "milliyet.com.tr", "milliyet", "sozcu.com.tr", "sozcu",
            "sabah.com.tr", "sabah spor", "takvim.com.tr", "takvim", "haberturk.com",
            "haberturk", "cnnturk.com", "cnnturk", "haberler.com", "haberler",
            "aspor.com.tr", "a spor", "trt.net.tr", "sporarena", "skorer"
        };

        // ── TIER 3 (70) — Yerel / bölgesel / genel haber portalı ───────────────────────
        private static readonly string[] Local = { "local", "regional", "gazete", "gazetesi", "ajans" };

        /// <summary>
        /// TIER-4 — DOĞRULANMAMIŞ kaynaklar: taraftar siteleri, forumlar, Reddit, X/Twitter benzeri
        /// sosyal ağlar, blog/aggregator platformları. Bu içerik MatchNewsArticles'ta SAKLANABİLİR
        /// (keşif/araştırma katmanı) ama <see cref="MatchIntelligenceService"/> kapısından geçemez →
        /// AI'ın factual match evidence'ına GİRMEZ.
        ///
        /// Whitelist'ten ÖNCE bakılır: bir sosyal/aggregator relay güvenilir bir yayıncının adını
        /// içerse bile (ör. "twitter.com/BBCSport") doğrulanmamış sayılır.
        /// </summary>
        private static readonly string[] Tier3Unverified =
        {
            "reddit", "twitter.com", "x.com/", "t.co/", "facebook", "instagram", "tiktok",
            "forum", "fanpage", "fansite", "fan site", "blogspot", "wordpress.com",
            "substack", "tumblr", "quora", "pinterest", "telegram", "vk.com",
            "medium.com", "youtube.com", "dailymotion"
        };

        /// <summary>Arama motoru relay'i — GERÇEK yayıncı değildir, kaliteye esas alınamaz.</summary>
        private static readonly string[] SearchRelay =
        {
            "news.google.com", "google news", "bing.com", "bing news", "news.yahoo",
            "msn.com", "flipboard"
        };

        /// <summary>Tier-3 eşiği: bu değerin altındaki hiçbir kaynak factual evidence olamaz.</summary>
        public const int UnverifiedQuality = 30;

        /// <summary>Resmi (federasyon / lig / kulüp) kaynak eşiği.</summary>
        public const int OfficialQuality = 95;

        public int Quality(string source) => Quality(source, null);

        /// <summary>
        /// Kaynak kalitesi. <paramref name="teamNames"/> verilirse RESMİ KULÜP tespiti de yapılır:
        /// domain'in ayırt edici parçası takım adını içeriyorsa (galatasaray.org, arsenal.com,
        /// fcbayern.com) bu bir kulüp resmi kaynağıdır → Tier-1. Böylece dünyanın herhangi bir
        /// kulübünün resmi sitesi, listeye tek tek yazılmadan yüksek güvenilirlik alır.
        /// </summary>
        public int Quality(string source, IEnumerable<string>? teamNames)
        {
            if (string.IsNullOrWhiteSpace(source)) return 50;
            var s = NewsTextNormalizer.Fold(source);

            // TIER-3 önce: doğrulanmamış kaynak hiçbir koşulda yükseltilmez.
            if (Tier3Unverified.Any(t => s.Contains(t, StringComparison.Ordinal)))
                return UnverifiedQuality;

            // Arama motoru relay'i gerçek yayıncı değildir → bilinmeyen seviyesinde kalır.
            if (SearchRelay.Any(t => s.Contains(t, StringComparison.Ordinal))) return 60;

            if (Federation.Any(t => s.Contains(t, StringComparison.Ordinal))) return OfficialQuality;
            if (LeagueOfficial.Any(t => s.Contains(t, StringComparison.Ordinal))) return OfficialQuality;
            if (OfficialClub.Any(t => s.Contains(t, StringComparison.Ordinal))) return OfficialQuality;

            if (MajorMedia.Any(t => s.Contains(t, StringComparison.Ordinal))) return 90;
            if (VerifiedSports.Any(t => s.Contains(t, StringComparison.Ordinal))) return 85;
            if (TurkishNational.Any(t => s.Contains(t, StringComparison.Ordinal))) return 85;

            // Bilinen medya değil → takımın KENDİ resmi domaini olabilir mi?
            if (teamNames != null && IsOfficialClubDomain(s, teamNames)) return OfficialQuality;

            if (Local.Any(t => s.Contains(t, StringComparison.Ordinal))) return 70;

            // Bilinmeyen yayıncı: blog/orta seviye.
            return 60;
        }

        /// <summary>Bir haberin tüm kaynaklarından en yüksek kaliteyi alır.</summary>
        public int BestQuality(IEnumerable<string> sources) => BestQuality(sources, null);

        public int BestQuality(IEnumerable<string> sources, IEnumerable<string>? teamNames)
        {
            var best = 50;
            foreach (var src in sources)
                best = Math.Max(best, Quality(src, teamNames));
            return best;
        }

        /// <summary>Kaynak resmi mi (federasyon / lig / kulüp)?</summary>
        public bool IsOfficial(string source, IEnumerable<string>? teamNames = null) =>
            Quality(source, teamNames) >= OfficialQuality;

        /// <summary>
        /// Domain, maçtaki takımlardan birinin RESMİ sitesi mi? "galatasaray.org.tr",
        /// "fcbayern.com", "arsenal.com" → evet. "galatasaray haberleri | hurriyet.com.tr"
        /// → hayır (hurriyet zaten whitelist'te yakalanır ve buraya düşmez).
        /// Yalnız domain'in kendisine bakılır; başlıkta takım adı geçmesi yetmez.
        /// </summary>
        private static bool IsOfficialClubDomain(string foldedSource, IEnumerable<string> teamNames)
        {
            // Kaynak alanı domain ya da yayıncı adı olabilir; her ikisinde de harf-dışı ayraçları at.
            var compact = NewsTextNormalizer.Alphanumeric(foldedSource);
            if (compact.Length == 0 || compact.Length > 40) return false;

            foreach (var team in teamNames)
            {
                foreach (var token in NewsTextNormalizer.TeamTokens(team))
                {
                    var t = NewsTextNormalizer.Alphanumeric(token);
                    if (t.Length < 5) continue;

                    // Domain takım adından ibaret (+ fc/cf/sk/official/com/org/tr ekleri).
                    if (!compact.Contains(t, StringComparison.Ordinal)) continue;
                    var rest = compact.Replace(t, "", StringComparison.Ordinal);
                    if (rest.Length <= 12) return true;
                }
            }
            return false;
        }
    }
}
