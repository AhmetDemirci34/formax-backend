using Formax.Application.Interfaces;
using Formax.Application.DTOs.Recommendations;
using Formax.Application.DTOs.Teams;
using Formax.Domain.Entities;

namespace Formax.Application.Services;

public class FirstSessionFeedService : IFirstSessionFeedService
{
    private readonly IMatchReadRepository _matchReadRepository;
    private readonly ITeamRepository _teamRepository;

    public FirstSessionFeedService(
        IMatchReadRepository matchReadRepository,
        ITeamRepository teamRepository)
    {
        _matchReadRepository = matchReadRepository;
        _teamRepository = teamRepository;
    }

    public async Task<List<RecommendationCardDto>> GetHookMatches(int userId)
    {
        var matches = _matchReadRepository.Query()
            .Take(10)
            .ToList();

        var result = new List<RecommendationCardDto>();

        foreach (var m in matches)
        {
            var homeTeam = _teamRepository.GetById(m.HomeTeamId);
            var awayTeam = _teamRepository.GetById(m.AwayTeamId);

            if (homeTeam == null)
                throw new Exception($"TEAM BROKEN → {m.HomeTeamId}");

            if (awayTeam == null)
                throw new Exception($"TEAM BROKEN → {m.AwayTeamId}");

            result.Add(new RecommendationCardDto
            {
                MatchId = m.Id,

                HomeTeam = new TeamDto
                {
                    Id = homeTeam.Id,
                    Name = homeTeam.Name,
                    LeagueRank = homeTeam.LeagueRank ?? 0,
                    AvgGoalsFor = homeTeam.AvgGoalsFor ?? 0,
                    AvgGoalsAgainst = homeTeam.AvgGoalsAgainst ?? 0,
                    IsStableTeam = homeTeam.IsStableTeam ?? false,
                    LogoUrl = homeTeam.LogoUrl,
                    ColorPrimary = homeTeam.ColorPrimary,
                    ColorSecondary = homeTeam.ColorSecondary
                },

                AwayTeam = new TeamDto
                {
                    Id = awayTeam.Id,
                    Name = awayTeam.Name,
                    LeagueRank = awayTeam.LeagueRank ?? 0,
                    AvgGoalsFor = awayTeam.AvgGoalsFor ?? 0,
                    AvgGoalsAgainst = awayTeam.AvgGoalsAgainst ?? 0,
                    IsStableTeam = awayTeam.IsStableTeam ?? false,
                    LogoUrl = awayTeam.LogoUrl,
                    ColorPrimary = awayTeam.ColorPrimary,
                    ColorSecondary = awayTeam.ColorSecondary
                },

                Score = 0,
                RecommendationScore = 0
            });
        }

        return result;
    }
}