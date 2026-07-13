using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Radar.Intelligence.Scenarios
{
    /// <summary>
    /// FORMAX Radar v2.2 — Dynamic Scenario Ranking.
    ///
    /// MarketProbabilityEngine'in ürettiği geniş havuzu puanlar ve:
    ///   1) her market AİLESİNDEN yalnız en güçlü adayı tutar
    ///      → çelişen senaryolar (2.5 Üst vs 2.5 Alt) ve tekrar eden marketler elenir,
    ///   2) kalan aile-temsilcilerini skora göre sıralar,
    ///   3) en güçlü ilk N senaryoyu döndürür.
    ///
    /// Sonuç her maçta farklı, çelişkisiz ve birbirini tekrar etmeyen bir top-3'tür.
    /// </summary>
    public sealed class ScenarioRankingService
    {
        public IReadOnlyList<ScenarioCandidate> RankTop(
            IReadOnlyList<ScenarioCandidate> candidates, int take = 3)
        {
            if (candidates == null || candidates.Count == 0)
                return new List<ScenarioCandidate>();

            // 1) Aile başına en yüksek skorlu tek aday (çelişki + tekrar filtresi).
            var perFamilyBest = candidates
                .GroupBy(c => c.Family)
                .Select(g => g.OrderByDescending(c => c.Score).First());

            // 2-3) Skora göre sırala, ilk N.
            return perFamilyBest
                .OrderByDescending(c => c.Score)
                .Take(take)
                .ToList();
        }
    }
}
