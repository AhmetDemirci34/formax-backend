using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases
{
    public sealed class GetMatchesUseCase
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly IUserInterestQueryService _interestQueryService;
        private readonly ISapmaMotor _sapmaMotor;

        public GetMatchesUseCase(
            IMatchReadRepository matchReadRepository,
            IUserInterestQueryService interestQueryService,
            ISapmaMotor sapmaMotor)
        {
            _matchReadRepository = matchReadRepository;
            _interestQueryService = interestQueryService;
            _sapmaMotor = sapmaMotor;
        }

        public async Task<IReadOnlyList<MatchListItemDto>> Handle(int? userId = null)
        {
            var matches = await _matchReadRepository.GetMatchListAsync();

            // --------------------------------------------------
            // FORMAX ANA REFERANS v1.1 — Sapma Motoru
            // Not: Sapma motoru userId'den bağımsızdır; login olmasa da üretir.
            // --------------------------------------------------
            foreach (var match in matches)
            {
                var s = _sapmaMotor.CalculateForListItem(match);

                match.OynanmaSkoru = s.OynanmaSkoru;
                match.GucSkoru = s.GucSkoru;
                match.Sapma = s.Sapma;

                match.OynanmaYonu = s.OynanmaYonu;
                match.GercekGucYonu = s.GercekGucYonu;

                match.SapmaBolgesi = s.SapmaBolgesi;
                match.SessizMi = s.SessizMi;
                match.SapmaMetni = s.SapmaMetni;

                match.OynanmaFreshness = s.OynanmaFreshness;
                match.OynanmaAgeSeconds = s.OynanmaAgeSeconds;
                match.AnalysisMuted = s.AnalysisMuted;
            }

            // Login değilse eski davranış
            if (!userId.HasValue)
                return matches;

            // Team interest skorları
            var teamInterests = await _interestQueryService.GetTopAsync(
                userId.Value,
                "Team",
                100);

            var teamScoreMap = teamInterests
                .ToDictionary(x => x.Key, x => x.Score);

            foreach (var match in matches)
            {
                int score = 0;

                if (teamScoreMap.TryGetValue(match.HomeTeam, out var homeScore))
                    score += homeScore * 10;

                if (teamScoreMap.TryGetValue(match.AwayTeam, out var awayScore))
                    score += awayScore * 10;

                match.RankScore = score;
            }

            return matches
                .OrderByDescending(m => m.RankScore)
                .ToList();
        }
    }
}