using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
            DateTime kickoffUtc, CancellationToken ct = default)
        {
            var query = _queryBuilder.Build(formaxMatchId, home, away, league, country, kickoffUtc);
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

            var candidates = (await Task.WhenAll(fetches)).SelectMany(x => x).ToList();
            var totalProviders = candidates.Select(c => c.Provider)
                                           .Distinct(StringComparer.OrdinalIgnoreCase).Count();

            // Duplicate detection → her grup tek habere iner.
            var groups = _dedup.Group(candidates);
            var homeL = query.HomeTeam.ToLowerInvariant();
            var awayL = query.AwayTeam.ToLowerInvariant();

            var items = new List<DedupedNewsItem>();
            foreach (var g in groups)
            {
                var rep = g.OrderByDescending(c => c.PublishedUtc).First();
                var sources = g.Select(c => string.IsNullOrWhiteSpace(c.Publisher) ? c.Provider : c.Publisher)
                               .Where(s => !string.IsNullOrWhiteSpace(s))
                               .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                var titleL = rep.Headline.ToLowerInvariant();
                var bothTeams = !string.IsNullOrEmpty(homeL) && !string.IsNullOrEmpty(awayL)
                                && titleL.Contains(homeL) && titleL.Contains(awayL);

                var clusters = _cluster.Classify(rep.Headline, rep.Summary).ToList();
                var confidence = _confidence.Score(sources.Count, rep.PublishedUtc, bothTeams);

                items.Add(new DedupedNewsItem
                {
                    FormaxMatchId = query.FormaxMatchId,
                    Headline = rep.Headline,
                    Summary = rep.Summary,
                    Url = rep.Url,
                    PublishedUtc = rep.PublishedUtc,
                    Language = rep.Language,
                    Sources = sources,
                    SourceCount = sources.Count,
                    Clusters = clusters,
                    Confidence = confidence,
                    ContentHash = Hash(query.FormaxMatchId + "|" + Normalize(rep.Headline))
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

        private static string Normalize(string s) =>
            new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        private static string Hash(string s)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes, 0, 12);
        }
    }
}
