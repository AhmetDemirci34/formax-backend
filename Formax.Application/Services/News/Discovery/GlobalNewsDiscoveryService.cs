using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.News.Discovery
{
    /// <summary>
    /// FORMAX Data Engine v2 — Global News Discovery orchestrator.
    ///
    /// FORMAX_MATCH_ID → query builder → açık provider'lar (paralel) → normalize →
    /// duplicate detection → clustering → confidence → MatchNewsContext.
    /// Mevcut Reasoning/LLM'e dokunmaz; yalnız haberi FORMAX_MATCH_ID altında toplar.
    /// </summary>
    public sealed class GlobalNewsDiscoveryService
    {
        private readonly IEnumerable<INewsProvider> _providers;
        private readonly MatchNewsSearchQueryBuilder _queryBuilder;
        private readonly NewsDuplicateDetector _dedup;
        private readonly NewsClusterEngine _cluster;
        private readonly NewsConfidenceEngine _confidence;
        private readonly ILogger<GlobalNewsDiscoveryService> _logger;

        public GlobalNewsDiscoveryService(
            IEnumerable<INewsProvider> providers,
            MatchNewsSearchQueryBuilder queryBuilder,
            NewsDuplicateDetector dedup,
            NewsClusterEngine cluster,
            NewsConfidenceEngine confidence,
            ILogger<GlobalNewsDiscoveryService> logger)
        {
            _providers = providers;
            _queryBuilder = queryBuilder;
            _dedup = dedup;
            _cluster = cluster;
            _confidence = confidence;
            _logger = logger;
        }

        public Task<(IReadOnlyList<DedupedNewsItem> Items, MatchNewsContext Context)> DiscoverForMatchAsync(
            string formaxMatchId, string home, string away, string league, string country,
            DateTime kickoffUtc, CancellationToken ct = default,
            int queryBudget = 0, bool urgent = false)
        {
            var query = _queryBuilder.Build(formaxMatchId, home, away, league, country, kickoffUtc);

            // Sorgu bütçesi maçın kickoff yakınlığından gelir (flash öncelik). Provider'lar
            // listenin yalnız ilk N sorgusunu çalıştırır → bütçe = tarama derinliği.
            query.QueryBudget = queryBudget;
            query.IsUrgent = urgent;

            return DiscoverAsync(query, ct);
        }

        public async Task<(IReadOnlyList<DedupedNewsItem> Items, MatchNewsContext Context)> DiscoverAsync(
            NewsQuery query, CancellationToken ct = default)
        {
            var enabled = _providers.Where(p => p.IsEnabled).ToList();

            var fetches = enabled.Select(async p =>
            {
                try { return await p.SearchAsync(query, ct); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NEWS] provider '{Provider}' başarısız", p.Name);
                    return (IReadOnlyList<NewsCandidate>)Array.Empty<NewsCandidate>();
                }
            });

            var fetched = (await Task.WhenAll(fetches)).SelectMany(x => x).ToList();
            var totalProviders = fetched.Select(c => c.Provider)
                                        .Distinct(StringComparer.OrdinalIgnoreCase).Count();

            // FUTBOL TRİYAJI KEŞİF ANINDA: başka spor dalı / reklam içeriği keşif katmanına
            // bile alınmaz. Ölçüldü — "Watch Shields vs Scott" (boks) gibi kayıtlar takım-açı
            // sorgularıyla geliyor ve depoyu kirletiyordu. Şüpheli değil, KESİN olanlar atılır;
            // haber-mi-değil-mi kararı yine Evidence kapılarında verilir.
            var candidates = fetched
                .Where(c => !Intelligence.MatchIntelligenceService
                                .IsForeignSportOrPromo(c.Headline + " " + c.Summary))
                .ToList();

            // Duplicate detection → her grup tek habere iner.
            var groups = _dedup.Group(candidates);

            var items = new List<DedupedNewsItem>();
            foreach (var g in groups)
            {
                // TEMSİLCİ: gerçek özeti olan kayıt öncelikli — anlatının dayanabileceği tek
                // malzeme odur. Eşitlikte en yeni.
                var rep = g.OrderByDescending(c => !string.IsNullOrWhiteSpace(c.Summary))
                           .ThenByDescending(c => c.PublishedUtc)
                           .First();

                // KAYNAK SAYISI = GERÇEK YAYINCI sayısı. Arama motorunun kendi adı ("Google
                // News") yayıncı değildir; yalnız gerçek yayıncı yoksa geriye düşülür.
                var publishers = g.Select(c => c.Publisher)
                                  .Where(s => !string.IsNullOrWhiteSpace(s))
                                  .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var sources = publishers.Count > 0
                    ? publishers
                    : g.Select(c => c.Provider).Where(s => !string.IsNullOrWhiteSpace(s))
                       .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                var bothTeams = Intelligence.MatchIntelligenceService.MentionsBothTeams(
                    rep.Headline + " " + rep.Summary, query.HomeTeam, query.AwayTeam);

                var clusters = _cluster.Classify(rep.Headline, rep.Summary).ToList();
                var confidence = _confidence.Score(publishers.Count, rep.PublishedUtc, bothTeams);

                items.Add(new DedupedNewsItem
                {
                    FormaxMatchId = query.FormaxMatchId,
                    Headline = rep.Headline,
                    Summary = rep.Summary,
                    Url = rep.Url,
                    PublishedUtc = rep.PublishedUtc,
                    Language = rep.Language,
                    Sources = sources,
                    SourceCount = Math.Max(1, publishers.Count),
                    Clusters = clusters,
                    Confidence = confidence,

                    // TEKİLLEŞTİRME ANAHTARI ARTIK OLAY DÜZEYİNDE. Eskiden ham başlığın
                    // normalizasyonuydu; aynı gelişmenin farklı yayıncıdaki başlığı
                    // ("Fenerbahçe'de X sakatlandı" / "X'ten kötü haber") ayrı kayıt oluyordu.
                    // Olay anahtarı anlam taşıyan kelimelerden türer → aynı olay tek satır.
                    ContentHash = Intelligence.NewsTextNormalizer.Hash(
                        query.FormaxMatchId + "|" +
                        Intelligence.NewsTextNormalizer.EventKey(rep.Headline))
                });
            }

            var context = BuildContext(query.FormaxMatchId, items, totalProviders);
            return (items, context);
        }

        private static MatchNewsContext BuildContext(string id, List<DedupedNewsItem> items, int providers)
        {
            var clusters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in items.SelectMany(i => i.Clusters))
                clusters[c] = clusters.TryGetValue(c, out var n) ? n + 1 : 1;

            return new MatchNewsContext
            {
                FormaxMatchId = id,
                TotalNews = items.Count,
                TotalProviders = providers,
                Clusters = clusters,
                TopHeadlines = items.OrderByDescending(i => i.Confidence).Take(5).Select(i => i.Headline).ToList(),
                LatestHeadlines = items.OrderByDescending(i => i.PublishedUtc).Take(5).Select(i => i.Headline).ToList(),
                Confidence = items.Count == 0 ? 0 : (int)Math.Round(items.Average(i => i.Confidence))
            };
        }

    }
}
