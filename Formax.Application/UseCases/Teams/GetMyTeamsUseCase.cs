using Formax.Application.DTOs.Teams;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Teams;

public class GetMyTeamsUseCase
{
    private readonly IUserTeamFollowRepository _follows;
    private readonly ITeamReadRepository _teams;

    public GetMyTeamsUseCase(
        IUserTeamFollowRepository follows,
        ITeamReadRepository teams)
    {
        _follows = follows;
        _teams = teams;
    }

    public async Task<List<TeamDto>> ExecuteAsync(int userId)
    {
        // ✅ SENİN REPOYA UYGUN
        var active = await _follows.GetActiveByUserAsync(userId);

        var ids = active
            .Select(x => x.TeamId)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new List<TeamDto>();

        // ✅ TeamReadRepository Query kullanıyoruz
        var teams = _teams.Query()
            .Where(t => ids.Contains(t.Id))
            .ToList();

        return teams
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
    }
}