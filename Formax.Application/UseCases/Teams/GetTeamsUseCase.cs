using Formax.Application.DTOs.Teams;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Teams;

public class GetTeamsUseCase
{
    private readonly ITeamReadRepository _teams;

    public GetTeamsUseCase(ITeamReadRepository teams)
    {
        _teams = teams;
    }

    public Task<List<TeamDto>> ExecuteAsync()
    {
        var list = _teams.Query().ToList();

        var result = list
            .OrderBy(t => t.Name)
            .Select(t => new TeamDto
            {
                Id = t.Id,
                Name = t.Name,

                LeagueRank = t.LeagueRank ?? 0,
                AvgGoalsFor = t.AvgGoalsFor ?? 0,
                AvgGoalsAgainst = t.AvgGoalsAgainst ?? 0,
                IsStableTeam = t.IsStableTeam ?? false,

                LogoUrl = t.LogoUrl,
                ColorPrimary = t.ColorPrimary,
                ColorSecondary = t.ColorSecondary
            })
            .ToList();

        return Task.FromResult(result);
    }
}