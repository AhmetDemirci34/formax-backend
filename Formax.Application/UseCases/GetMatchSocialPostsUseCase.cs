using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Social;
using Formax.Application.Interfaces;
using Formax.Application.Services.Fixtures;

namespace Formax.Application.UseCases
{
    /// <summary>
    /// Phase 7 — Match Detail > Flash Gelişmeler / Resmi Paylaşımlar. Canonical SocialPost'tan
    /// (AI Context ile AYNI kaynak) maçın resmi sosyal paylaşımlarını kullanıcı için döner.
    /// Coverage yoksa boş liste (fake yok).
    /// </summary>
    public sealed class GetMatchSocialPostsUseCase
    {
        private readonly IMatchReadRepository _matchRepo;
        private readonly ITeamReadRepository _teamRepo;
        private readonly ISocialPostRepository _socialRepo;
        private readonly FormaxMatchIdFactory _idFactory;

        public GetMatchSocialPostsUseCase(
            IMatchReadRepository matchRepo,
            ITeamReadRepository teamRepo,
            ISocialPostRepository socialRepo,
            FormaxMatchIdFactory idFactory)
        {
            _matchRepo = matchRepo;
            _teamRepo = teamRepo;
            _socialRepo = socialRepo;
            _idFactory = idFactory;
        }

        public List<MatchSocialPostDto> Execute(int matchId)
        {
            var match = _matchRepo.GetById(matchId);
            if (match == null) return new List<MatchSocialPostDto>();

            var home = _teamRepo.GetById(match.HomeTeamId)?.Name ?? "";
            var away = _teamRepo.GetById(match.AwayTeamId)?.Name ?? "";

            string formaxMatchId;
            try { formaxMatchId = _idFactory.Create(match.MatchDate, home, away); }
            catch { return new List<MatchSocialPostDto>(); }

            return _socialRepo.GetByMatch(formaxMatchId, 40)
                .Select(p => new MatchSocialPostDto
                {
                    Headline = p.Headline,
                    Summary = p.Summary,
                    Source = string.IsNullOrWhiteSpace(p.AccountName) ? p.AccountHandle : p.AccountName,
                    Platform = p.Platform,
                    PublishedAt = p.PublishedUtc,
                    Url = p.Url,
                    SignalType = p.SignalType,
                    IsOfficial = p.IsOfficial
                })
                .ToList();
        }
    }
}
