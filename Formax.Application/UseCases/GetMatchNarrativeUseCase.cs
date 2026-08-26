using Formax.Application.AI.Context;
using Formax.Application.AI.LLM;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;
using Formax.Application.Services.Radar.Intelligence.Scenarios;

namespace Formax.Application.UseCases
{
    /// <summary>
    /// Football Intelligence v2 — LLM Narrative Intelligence diagnostic/surface.
    /// Bir maç için Unified AI Context'i kurar, motorun AiDecisionPackage'ini alır ve
    /// FootballNarrativeComposer ile YALNIZ FootballIntelligence bloğundan doğal Türkçe yorum üretir.
    /// Motor/olasılık/hash DEĞİŞMEZ (composer yalnız okur). Surface: DiscoverCard | MatchDetail | AiIncele.
    /// </summary>
    public sealed class GetMatchNarrativeUseCase
    {
        private readonly IMatchReadRepository _matchRepo;
        private readonly ITeamReadRepository _teamRepo;
        private readonly IMatchAiContextBuilder _builder;
        private readonly MarketProbabilityEngine _engine;
        private readonly FootballNarrativeComposer _composer = new();

        public GetMatchNarrativeUseCase(
            IMatchReadRepository matchRepo, ITeamReadRepository teamRepo,
            IMatchAiContextBuilder builder, MarketProbabilityEngine engine)
        {
            _matchRepo = matchRepo;
            _teamRepo = teamRepo;
            _builder = builder;
            _engine = engine;
        }

        public object? Execute(int matchId, FootballNarrativeSurface surface)
        {
            var match = _matchRepo.GetById(matchId);
            if (match == null) return null;

            var homeName = _teamRepo.GetById(match.HomeTeamId)?.Name ?? "Ev sahibi";
            var awayName = _teamRepo.GetById(match.AwayTeamId)?.Name ?? "Deplasman";

            // FAZ 1 — gerçek TeamComparison/H2H/GücSkoru builder tarafından üretilir (boş DTO kalktı).
            var context = _builder.Build(
                match.Id, match.HomeTeamId, match.AwayTeamId, homeName, awayName);

            var pkg = _engine.BuildDecisionPackage(context);
            var narrative = _composer.Compose(pkg, surface);

            return new
            {
                matchId,
                surface = surface.ToString(),
                narrative,
                footballIntelligence = pkg.FootballIntelligence,
                determinismHash = pkg.Meta.DeterminismHash
            };
        }
    }
}
