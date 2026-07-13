using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;

public class GetFollowedMatchesUseCase
{
    private readonly IUserMatchFollowRepository _repository;
    private readonly IMatchReadRepository _matchRepository;
    private readonly ISapmaMotor _sapmaMotor;

    public GetFollowedMatchesUseCase(
        IUserMatchFollowRepository repository,
        IMatchReadRepository matchRepository,
        ISapmaMotor sapmaMotor)
    {
        _repository    = repository;
        _matchRepository = matchRepository;
        _sapmaMotor    = sapmaMotor;
    }

    public async Task<List<MatchListItemDto>> ExecuteAsync(int userId)
    {
        var followedIds = (await _repository.GetByUserAsync(userId))
            .Select(x => x.MatchId)
            .ToList();

        if (followedIds.Count == 0)
            return new List<MatchListItemDto>();

        // Targeted SQL: WHERE Id IN (...) — fetches only followed rows
        var result = (await _matchRepository.GetMatchListByIdsAsync(followedIds)).ToList();

        foreach (var match in result)
        {
            var s = _sapmaMotor.CalculateForListItem(match);
            match.OynanmaSkoru     = s.OynanmaSkoru;
            match.GucSkoru         = s.GucSkoru;
            match.Sapma            = s.Sapma;
            match.OynanmaYonu      = s.OynanmaYonu;
            match.GercekGucYonu    = s.GercekGucYonu;
            match.SapmaBolgesi     = s.SapmaBolgesi;
            match.SessizMi         = s.SessizMi;
            match.SapmaMetni       = s.SapmaMetni;
            match.OynanmaFreshness = s.OynanmaFreshness;
            match.OynanmaAgeSeconds = s.OynanmaAgeSeconds;
            match.AnalysisMuted    = s.AnalysisMuted;
        }

        return result;
    }
}
