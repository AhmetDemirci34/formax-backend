using Formax.Application.DTOs.Home;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Recommendation;

public class AdaptiveInterestLearningService
{
    private readonly IUserInterestScoreRepository _repository;

    public AdaptiveInterestLearningService(IUserInterestScoreRepository repository)
    {
        _repository = repository;
    }

    // 🔥 FINAL: TEAM + LEAGUE BASED LEARNING (DEBUG DAHİL)
    public async Task UpdateFromEvent(int userId, FeedInteractionEvent e, double reward)
    {
        

        var matchId = e.MatchId;

        // 🔥 GEÇİCİ MAP (tüm matchId’leri ekle!)
        var teamMap = new Dictionary<int, (string home, string away, string league)>
        {
            {1, ("Galatasaray", "Fenerbahçe", "SuperLig")},
            {2, ("Fenerbahçe", "Beşiktaş", "SuperLig")},
            {3, ("Beşiktaş", "Trabzonspor", "SuperLig")},
            {4, ("Trabzonspor", "Galatasaray", "SuperLig")},
            {5, ("Galatasaray", "Beşiktaş", "SuperLig")}
        };

        if (!teamMap.ContainsKey(matchId))
        {
            
            return;
        }

        var match = teamMap[matchId];


        // 🔥 HOME TEAM
        await _repository.UpsertAsync(new UserInterestScore
        {
            UserId = userId,
            Layer = "team",
            Key = match.home,
            Score = (int)reward,
            LastEventAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        

        // 🔥 AWAY TEAM
        await _repository.UpsertAsync(new UserInterestScore
        {
            UserId = userId,
            Layer = "team",
            Key = match.away,
            Score = (int)reward,
            LastEventAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });


        // 🔥 LEAGUE
        await _repository.UpsertAsync(new UserInterestScore
        {
            UserId = userId,
            Layer = "league",
            Key = match.league,
            Score = (int)reward,
            LastEventAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

      
    }

    // 🔥 SCORING BOOST (DEĞİŞMEDİ)
    public double ApplyUserLearning(
        HomeRadarMatchDto match,
        double baseScore)
    {
        double boost = 0;

        boost += match.TeamInterestScore * 0.20;
        boost += match.LeagueInterestScore * 0.15;
        boost += match.ContentInterestScore * 0.10;
        boost += match.BehaviorMomentumScore * 0.25;

        return baseScore + boost;
    }
}