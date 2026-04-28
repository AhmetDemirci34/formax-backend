using Formax.Application.Interfaces.Repositories;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Intelligence;

public class UserTrendService
{
    private readonly IUserActionRepository _repo;
    private static readonly Random _random = new();

    public UserTrendService(IUserActionRepository repo)
    {
        _repo = repo;
    }

    public async Task<double> Calculate(int userId, int matchId, string teamA, string teamB, double currentOdds)
    {
        var actions = await _repo.GetByUserIdAsync(userId);

        if (actions.Count == 0)
            return 0.3;

        // 🔹 BASE USER (çok düşük etki)
        double totalScore = 0;

        foreach (var a in actions)
        {
            double score = 0;

            if (a.ViewDurationMs > 0)
            {
                if (a.ViewDurationMs < 2000) score = 0.2;
                else if (a.ViewDurationMs < 5000) score = 0.5;
                else if (a.ViewDurationMs < 10000) score = 0.7;
                else score = 1.0;

                if (a.OpenedDetail) score += 0.2;
                if (a.Followed) score += 0.3;
            }
            else
            {
                if (a.ActionType == 1) score = 0.8;
                else if (a.ActionType == -1) score = 0.1;
                else score = 0.5;
            }

            totalScore += Math.Clamp(score, 0, 1);
        }

        var userBaseScore = totalScore / actions.Count;

        // 🔹 MATCH BOOST
        double matchBoost = 0;

        var matchActions = actions
            .Where(x => x.MatchId == matchId)
            .ToList();

        if (matchActions.Count > 0)
        {
            matchBoost = matchActions
                .Select(x =>
                {
                    if (x.ViewDurationMs > 8000) return 0.3;
                    if (x.ActionType == 1) return 0.25;
                    return 0.05;
                })
                .Max();
        }

        // 🔥 TEAM BOOST (MAX + INTENSITY)
        double teamBoost = 0;

        var teamActions = actions
            .Where(x => x.Team == teamA || x.Team == teamB)
            .ToList();

        if (teamActions.Count > 0)
        {
            var baseBoost = teamActions
                .Select(x =>
                {
                    double score = 0.1;

                    if (x.ViewDurationMs > 8000) score += 0.4;
                    if (x.ActionType == 1) score += 0.3;
                    if (x.Followed) score += 0.2;

                    return score;
                })
                .Max();

            var intensity = teamActions.Count;

            // 🔥 kritik fark
            teamBoost = baseBoost * (1 + Math.Min(intensity * 0.15, 1.5));
        }

        // 🔥 ODDS BEHAVIOR
        double oddsBoost = 0;

        var similarOdds = actions
            .Where(x => Math.Abs(x.Odds - currentOdds) < 0.3)
            .ToList();

        if (similarOdds.Count > 0)
        {
            oddsBoost = similarOdds
                .Select(x =>
                {
                    if (x.ActionType == 1) return 0.3;
                    if (x.ActionType == -1) return 0.1;
                    return 0.2;
                })
                .Max();
        }

        // 🔥 RECENCY
        double recencyBoost = 0;

        var recent = actions
            .Where(x => x.CreatedAt > DateTime.UtcNow.AddHours(-6))
            .ToList();

        if (recent.Count > 0)
        {
            recencyBoost = recent
                .Select(x =>
                {
                    if (x.ActionType == 1) return 0.3;
                    return 0.1;
                })
                .Max();
        }

        // 🔥 EXPLORATION (kontrollü randomness)
        double exploration = _random.NextDouble() * 0.25;

        // 🔥 FINAL SCORE (AGRESİF + AKILLI)
        var final =
            (teamBoost * 0.65) +   // 🔥 ana sinyal
            (oddsBoost * 0.10) +
            (recencyBoost * 0.10) +
            (matchBoost * 0.10) +
            (userBaseScore * 0.03) +
            exploration;

        return Math.Tanh(final);
    }
}