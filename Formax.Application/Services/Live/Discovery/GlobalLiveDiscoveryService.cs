using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Live.Discovery
{
    /// <summary>
    /// FORMAX Live Data Engine — Global Live Discovery orchestrator.
    ///
    /// Maç → query builder → açık provider'lar (paralel, dayanıklı) → skor çıkarımı →
    /// kaynaklar arası uzlaştırma → confidence → <see cref="DiscoveredLiveSignal"/>.
    /// GlobalNewsDiscoveryService ile aynı desen. Motora/DTO'ya/endpoint'e dokunmaz;
    /// yalnız açık kaynaklardan canlı skoru toplar. Tek üçüncü-taraf API'ye bağımlı değil.
    /// </summary>
    public sealed class GlobalLiveDiscoveryService
    {
        private readonly IEnumerable<ILiveSignalProvider> _providers;
        private readonly LiveSignalQueryBuilder _queryBuilder;
        private readonly LiveScoreExtractor _extractor;
        private readonly ILogger<GlobalLiveDiscoveryService> _logger;

        public GlobalLiveDiscoveryService(
            IEnumerable<ILiveSignalProvider> providers,
            LiveSignalQueryBuilder queryBuilder,
            LiveScoreExtractor extractor,
            ILogger<GlobalLiveDiscoveryService> logger)
        {
            _providers = providers;
            _queryBuilder = queryBuilder;
            _extractor = extractor;
            _logger = logger;
        }

        public Task<DiscoveredLiveSignal> DiscoverForMatchAsync(
            int matchId, string home, string away, string league, string country,
            DateTime kickoffUtc, CancellationToken ct = default)
        {
            var query = _queryBuilder.Build(matchId, home, away, league, country, kickoffUtc);
            return DiscoverAsync(query, ct);
        }

        public async Task<DiscoveredLiveSignal> DiscoverAsync(LiveSignalQuery query, CancellationToken ct = default)
        {
            var enabled = _providers.Where(p => p.IsEnabled).ToList();
            if (enabled.Count == 0) return DiscoveredLiveSignal.None(query.MatchId);

            var fetches = enabled.Select(async p =>
            {
                try { return await p.FetchAsync(query, ct); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[LIVE DISC] provider '{Provider}' başarısız", p.Name);
                    return (IReadOnlyList<LiveSignalCandidate>)Array.Empty<LiveSignalCandidate>();
                }
            });

            var candidates = (await Task.WhenAll(fetches)).SelectMany(x => x).ToList();
            if (candidates.Count == 0) return DiscoveredLiveSignal.None(query.MatchId);

            // Canlı sinyal hızlı bayatlar: yalnız son 3 saatteki adayları dikkate al.
            var cutoff = DateTime.UtcNow.AddHours(-3);
            var fresh = candidates.Where(c => c.PublishedUtc >= cutoff).ToList();
            if (fresh.Count == 0) return DiscoveredLiveSignal.None(query.MatchId);

            // Skoru net belirten adaylardan çıkarım yap.
            var scored = new List<(int H, int A, DateTime Pub, string Source, string Text)>();
            foreach (var c in fresh)
            {
                var text = (c.Headline + " " + c.Summary).Trim();
                if (_extractor.TryExtract(text, query.HomeTeam, query.AwayTeam, out var h, out var a))
                {
                    // Bağımsızlık ölçütü: gerçek yayıncı (BBC/ESPN...); yoksa provider adı.
                    var source = string.IsNullOrWhiteSpace(c.Publisher) ? c.Provider : c.Publisher;
                    scored.Add((h, a, c.PublishedUtc, source, text));
                }
            }
            if (scored.Count == 0) return DiscoveredLiveSignal.None(query.MatchId);

            // Uzlaştırma: en güncel sinyalin skorunu al; kaç bağımsız yayıncı doğruladı say.
            var latest = scored.OrderByDescending(s => s.Pub).First();
            var agree = scored.Where(s => s.H == latest.H && s.A == latest.A).ToList();
            var distinctProviders = agree
                .Select(s => s.Source)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var minute = _extractor.TryExtractMinute(latest.Text);
            var confidence = ScoreConfidence(distinctProviders, latest.Pub);

            return new DiscoveredLiveSignal
            {
                MatchId = query.MatchId,
                HasScore = true,
                HomeScore = latest.H,
                AwayScore = latest.A,
                Minute = minute,
                Confidence = confidence,
                SourceCount = distinctProviders,
                Events = Array.Empty<DiscoveredLiveEvent>()
            };
        }

        /// <summary>Kaynak sayısı + tazelik → 0..100 güven.</summary>
        private static int ScoreConfidence(int sources, DateTime pubUtc)
        {
            var ageMin = (DateTime.UtcNow - pubUtc).TotalMinutes;
            int freshness = ageMin <= 10 ? 40 : ageMin <= 30 ? 30 : ageMin <= 90 ? 20 : 10;
            int src = Math.Min(50, sources * 25);
            return Math.Min(100, 20 + freshness + src);
        }
    }
}
