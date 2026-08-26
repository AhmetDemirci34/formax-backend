using Formax.Application.AI.Context;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;
using Formax.Application.Services.Odds;
using Formax.Application.Services.Radar.Intelligence.Scenarios;

namespace Formax.Application.UseCases
{
    /// <summary>
    /// FORMAX BEYNİ — MarketProbabilityEngine.BuildDecisionPackage diagnostic/introspection.
    /// Bir maç için Unified AI Context'i (gerçek GDP repo'larından) kurar ve motorun ürettiği
    /// AI Decision Package'i döner. Motor YALNIZ context okur.
    ///
    /// Paket kurulduktan SONRA her olasılık kalemine GERÇEK market oranı (MatchMarketOdds)
    /// enjekte edilir. Motor oran görmez → determinizm bozulmaz; oran yalnız taşınır.
    /// Sağlayıcıda karşılığı olmayan market oransız kalır (uydurulmaz).
    /// </summary>
    public sealed class GetMatchDecisionUseCase
    {
        private readonly IMatchReadRepository _matchRepo;
        private readonly ITeamReadRepository _teamRepo;
        private readonly IMatchAiContextBuilder _builder;
        private readonly MarketProbabilityEngine _engine;
        private readonly IMatchOddsRepository _oddsRepo;

        public GetMatchDecisionUseCase(
            IMatchReadRepository matchRepo,
            ITeamReadRepository teamRepo,
            IMatchAiContextBuilder builder,
            MarketProbabilityEngine engine,
            IMatchOddsRepository oddsRepo)
        {
            _matchRepo = matchRepo;
            _teamRepo = teamRepo;
            _builder = builder;
            _engine = engine;
            _oddsRepo = oddsRepo;
        }

        public async Task<object?> ExecuteAsync(int matchId, CancellationToken ct = default)
        {
            var match = _matchRepo.GetById(matchId);
            if (match == null) return null;

            var homeName = _teamRepo.GetById(match.HomeTeamId)?.Name ?? "Ev sahibi";
            var awayName = _teamRepo.GetById(match.AwayTeamId)?.Name ?? "Deplasman";

            // FAZ 1 — gerçek TeamComparison/H2H/GücSkoru builder tarafından üretilir (boş DTO kalktı).
            var context = _builder.Build(
                match.Id, match.HomeTeamId, match.AwayTeamId, homeName, awayName);

            var package = _engine.BuildDecisionPackage(context);

            // GERÇEK oran enjeksiyonu — market adı → oran anahtarı → MatchMarketOdds satırı.
            var oddRows = await _oddsRepo.GetByMatchAsync(matchId, ct);
            if (oddRows.Count > 0)
            {
                var byKey = oddRows.ToDictionary(r => r.MarketKey, StringComparer.Ordinal);
                foreach (var p in package.Probabilities)
                {
                    var key = DecisionMarketOddsMapper.ToOddsKey(p.Market);
                    if (key == null) continue;
                    if (!byKey.TryGetValue(key, out var row)) continue;
                    p.Odd = row.Odd;
                    p.PreviousOdd = row.PreviousOdd;
                }
            }

            return package;
        }
    }
}
