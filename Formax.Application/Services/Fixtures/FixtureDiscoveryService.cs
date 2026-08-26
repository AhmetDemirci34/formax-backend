using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Fixtures
{
    /// <summary>
    /// FORMAX Data Engine v1 — Fixture Discovery Engine (orchestrator).
    ///
    /// Global Sources → Discovery → Normalization → Identity → Confidence → FORMAX_MATCH_ID.
    /// Tüm açık provider'ları çalıştırır, adayları kanonik kimliğe (takım+lig) indirger,
    /// aynı FORMAX_MATCH_ID altında birleştirir, çok-kaynak güveni hesaplar ve yalnız
    /// yeterli güvendeki maçları döndürür. Dış API MatchId'sine bağımlı değildir.
    /// </summary>
    public sealed class FixtureDiscoveryService
    {
        private readonly IEnumerable<IFixtureProvider> _providers;
        private readonly TeamIdentityResolver _teams;
        private readonly LeagueIdentityResolver _leagues;
        private readonly FormaxMatchIdFactory _idFactory;
        private readonly FixtureConfidenceEngine _confidence;
        private readonly ILogger<FixtureDiscoveryService> _logger;

        public FixtureDiscoveryService(
            IEnumerable<IFixtureProvider> providers,
            TeamIdentityResolver teams,
            LeagueIdentityResolver leagues,
            FormaxMatchIdFactory idFactory,
            FixtureConfidenceEngine confidence,
            ILogger<FixtureDiscoveryService> logger)
        {
            _providers = providers;
            _teams = teams;
            _leagues = leagues;
            _idFactory = idFactory;
            _confidence = confidence;
            _logger = logger;
        }

        public async Task<IReadOnlyList<FixtureDiscoveryResult>> DiscoverAsync(
            DateOnly fromUtc, DateOnly toUtc, int minConfidence = 60, CancellationToken ct = default)
        {
            var enabled = _providers.Where(p => p.IsEnabled).ToList();

            var fetches = enabled.Select(async p =>
            {
                try { return await p.DiscoverAsync(fromUtc, toUtc, ct); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[FIXTURE] provider '{Provider}' başarısız", p.Name);
                    return (IReadOnlyList<FixtureCandidate>)Array.Empty<FixtureCandidate>();
                }
            });

            var candidates = (await Task.WhenAll(fetches)).SelectMany(x => x).ToList();
            _logger.LogInformation("[FIXTURE] {Count} aday, {Sources} kaynaktan toplandı",
                candidates.Count, enabled.Count);

            // Normalize + FORMAX_MATCH_ID'ye göre grupla.
            var groups = new Dictionary<string, List<NormalizedCandidate>>(StringComparer.Ordinal);
            foreach (var c in candidates)
            {
                var home = _teams.Resolve(c.HomeTeam);
                var away = _teams.Resolve(c.AwayTeam);
                if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) continue;

                var league = _leagues.Resolve(c.League);
                // Kimlik lig-bağımsızdır (tek kimlik otoritesi); lig yalnız sonuç meta verisi olarak taşınır.
                var id = _idFactory.Create(c.DateUtc, home, away);

                if (!groups.TryGetValue(id, out var list))
                    groups[id] = list = new List<NormalizedCandidate>();

                list.Add(new NormalizedCandidate(c, home, away, league));
            }

            // Her grup için güven + tek sonuç.
            var results = new List<FixtureDiscoveryResult>();
            foreach (var (id, items) in groups)
            {
                var confidence = _confidence.Score(items.Select(i => i.Raw));
                if (confidence < minConfidence) continue; // teyitsiz/zayıf → maç oluşturma

                // Temsilci aday: en yüksek provider önceliği, eşitlikte kaynak güveni.
                var lead = items
                    .OrderByDescending(i => i.Raw.ProviderPriority)
                    .ThenByDescending(i => i.Raw.SourceConfidence)
                    .First();

                results.Add(new FixtureDiscoveryResult
                {
                    FormaxMatchId = id,
                    League = lead.League,
                    Country = _leagues.Country(lead.Raw.League, lead.Raw.Country),
                    Season = lead.Raw.Season,
                    Round = lead.Raw.Round,
                    KickoffUtc = lead.Raw.DateUtc,
                    HomeTeam = lead.Home,
                    AwayTeam = lead.Away,
                    Venue = items.Select(i => i.Raw.Venue).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)),
                    Status = MostCommonStatus(items),
                    Confidence = confidence,
                    Sources = items.Select(i => i.Raw.Source)
                                   .Distinct(StringComparer.OrdinalIgnoreCase)
                                   .OrderBy(s => s).ToList()
                });
            }

            return results.OrderByDescending(r => r.Confidence).ThenBy(r => r.KickoffUtc).ToList();
        }

        private static string MostCommonStatus(List<NormalizedCandidate> items) =>
            items.Select(i => i.Raw.Status)
                 .Where(s => !string.IsNullOrWhiteSpace(s))
                 .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                 .OrderByDescending(g => g.Count())
                 .Select(g => g.Key)
                 .FirstOrDefault() ?? "NotStarted";

        private readonly record struct NormalizedCandidate(
            FixtureCandidate Raw, string Home, string Away, string League);
    }
}
