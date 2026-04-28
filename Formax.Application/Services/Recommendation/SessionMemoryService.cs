using Formax.Domain.Entities;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.Recommendation;

public class SessionMemoryService
{
    private readonly IUserSessionInterestRepository _repo;

    public SessionMemoryService(IUserSessionInterestRepository repo)
    {
        _repo = repo;
    }

    public async Task UpdateFromEvent(int userId, string? league, string? team, string eventType)
    {
        var entity = await _repo.GetAsync(userId, league, team);

        if (entity == null)
        {
            entity = new UserSessionInterest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                League = league,
                Team = team,
                Clicks = 0,
                Dwells = 0,
                LastInteractionAt = DateTime.UtcNow
            };
        }

        // 🔥 EVENT BASED UPDATE
        switch (eventType)
        {
            case "Click":
                entity.Clicks += 1;
                break;

            case "Dwell":
                entity.Dwells += 1;
                break;

            case "Skip":
                entity.Clicks = Math.Max(0, entity.Clicks - 1);
                entity.Dwells = Math.Max(0, entity.Dwells - 1);
                break;
        }

        entity.LastInteractionAt = DateTime.UtcNow;

        await _repo.UpsertAsync(entity);
    }

    // 🔥 FINAL BOOST (DECAY + NORMALIZED)
    public async Task<double> GetBoost(int userId, string? league, string? team)
    {
        var entity = await _repo.GetAsync(userId, league, team);

        if (entity == null)
            return 0;

        // =========================
        // BASE SCORE
        // =========================
        double score =
            (entity.Clicks * 2.0) +
            (entity.Dwells * 1.5);

        // =========================
        // 🔥 TIME DECAY (KRİTİK)
        // =========================
        var minutes = (DateTime.UtcNow - entity.LastInteractionAt).TotalMinutes;

        double decay =
            minutes < 30 ? 1.0 :
            minutes < 120 ? 0.9 :
            minutes < 360 ? 0.75 :
            minutes < 720 ? 0.6 :
            0.4;

        score *= decay;

        // =========================
        // 🔥 NORMALIZE (PATLAMAYI ENGELLE)
        // =========================
        score = Math.Min(score, 15);

        return score;
    }
}