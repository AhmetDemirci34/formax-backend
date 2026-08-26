using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Nabiz;
using Formax.Application.Interfaces;
using Formax.Application.Services.News.Intelligence;

namespace Formax.Application.Services.News.Feed
{
    /// <summary>
    /// SON DAKİKA listesinin TEK üretim yeri.
    ///
    /// Hem maç detayı (<c>/detail</c> içindeki NabizFeed) hem de dile duyarlı haber ucu
    /// (<c>/news</c>) buradan beslenir → aynı maç için iki farklı haber listesi oluşamaz.
    ///
    /// KAYNAK: Data Engine v2'nin MatchNewsArticles deposu (FORMAX_MATCH_ID altında,
    /// ContentHash ile tekil). Yeni provider/toplama YOKTUR.
    ///
    /// KAPSAM: ham depo bilerek FİLTRESİZ keşif katmanıdır. Bu yüzden okuma yolunda
    /// <see cref="MatchIntelligenceService"/> içindeki MEVCUT public static kapılar
    /// uygulanır (NewsDiscoveryJob → BuildEvidence ile aynı metotlar, aynı sıra).
    /// Kaynak kalitesi kapısı UYGULANMAZ: o kapı AI'ın kanıt katmanı içindir; Son Dakika
    /// bir haber listesidir, Tier-3 yayıncı haberi kullanıcıdan gizlenmez.
    /// </summary>
    public sealed class MatchNewsFeedService
    {
        /// <summary>Son Dakika listesinde gösterilecek en fazla haber sayısı.</summary>
        public const int FeedLimit = 25;

        /// <summary>Kapılardan eleneceği için ham havuz geniş okunur.</summary>
        private const int RawReadLimit = 120;

        private readonly IMatchNewsRepository _news;

        public MatchNewsFeedService(IMatchNewsRepository news)
        {
            _news = news;
        }

        public async Task<List<NabizFeedItemDto>> BuildAsync(
            string formaxMatchId,
            string homeTeamName,
            string awayTeamName,
            DateTime kickoffUtc,
            CancellationToken ct = default)
        {
            var result = new List<NabizFeedItemDto>();
            if (string.IsNullOrWhiteSpace(formaxMatchId)) return result;

            var articles = await _news.GetArticlesAsync(formaxMatchId, RawReadLimit, ct);
            if (articles.Count == 0) return result;

            var teams = new[] { homeTeamName, awayTeamName };

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in articles.OrderByDescending(x => x.PublishedUtc))
            {
                if (result.Count >= FeedLimit) break;

                var text = (a.Headline ?? "") + " " + (a.Summary ?? "");

                // MEVCUT KAPILAR — yeni filtre yazılmaz.
                if (!MatchIntelligenceService.IsFootballRelevant(text, teams)) continue;
                if (MatchIntelligenceService.IsNonFactualContent(a.Headline, a.Summary)) continue;
                if (MatchIntelligenceService.IsBareFixtureTitle(a.Headline, a.Summary, homeTeamName, awayTeamName)) continue;
                if (MatchIntelligenceService.IsHistoricalContent(text, kickoffUtc)) continue;

                // MAÇ BAĞLAMA — KATI MOD (strictBothTeams: true).
                //
                // Haber, ancak metninde HEM ev sahibi HEM deplasman geçiyorsa bu maça aittir
                // (Relation.DirectMatch). Tek takım adına dayalı eşleştirme YAPILMAZ.
                //
                // NEDEN (ölçüldü 18.08): gevşek modda tek takım haberi takımın "sıradaki"
                // maçına bağlanıyordu. Fenerbahçe–Lyon maçı başlayınca sıradaki maç
                // Fenerbahçe–Konyaspor oldu ve Lyon maçının haberleri Konyaspor fikstürüne
                // kaydı ("Fenerbahçe, UEFA'ya Lyon maçları kadrosunu bildirdi" → Konyaspor
                // Son Dakika'sında). Ayrıca "Fenerbahçe yeni transfer yaptı" gibi maçla
                // ilgisiz takım haberleri de listeye giriyordu.
                //
                // KURAL: haberin hangi maça ait olduğu KESİN değilse GÖSTERİLMEZ. Boş Son
                // Dakika, yanlış maçın haberini göstermekten doğrudur.
                var relation = MatchIntelligenceService.ResolveRelation(
                    text, homeTeamName, awayTeamName, strictBothTeams: true,
                    kickoffUtc: kickoffUtc, publishedUtc: a.PublishedUtc,
                    // Katı modda bu bayraklar hiç okunmaz (tek takım yolu kapalı).
                    homeIsNextMatch: true, awayIsNextMatch: true);
                if (relation == null) continue;

                // Kimlik: kanonik URL varsa o, yoksa depo tekilleştirme anahtarı.
                var dedupeKey = string.IsNullOrWhiteSpace(a.Url) ? a.ContentHash : a.Url.Trim();
                if (!seen.Add(dedupeKey)) continue;

                result.Add(new NabizFeedItemDto
                {
                    // Kimlik = haberin depo anahtarı. Çeviri önbelleği ve UI seçimi bunu kullanır.
                    Id             = a.ContentHash,
                    Type           = relation,
                    Source         = FirstToken(a.Sources, string.Empty),
                    Author         = string.Empty,
                    AuthorVerified = false,
                    Headline       = a.Headline,
                    Summary        = string.IsNullOrWhiteSpace(a.Summary) ? null : a.Summary,
                    // Haber hattı görsel URL'si TAŞIMIYOR (ne DedupedNewsItem'da ne tabloda alan
                    // var). Görsel UYDURULMAZ; alan boş kalır, UI görselsiz kompakt render eder.
                    ImageUrl       = null,
                    SourceUrl      = a.Url ?? string.Empty,
                    PublishedAt    = a.PublishedUtc,
                    // Haberin KENDİ dili — çeviri gerekip gerekmediğini bu belirler.
                    Language       = string.IsNullOrWhiteSpace(a.Language) ? "en" : a.Language.Trim().ToLowerInvariant()
                });
            }

            return result;
        }

        /// <summary>Virgülle ayrılmış listenin ilk anlamlı ögesi; yoksa varsayılan.</summary>
        private static string FirstToken(string? csv, string fallback)
        {
            if (string.IsNullOrWhiteSpace(csv)) return fallback;
            foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = part.Trim();
                if (t.Length > 0) return t;
            }
            return fallback;
        }
    }
}
