using Formax.Application.DTOs.Home;
using Formax.Application.DTOs.Recommendations;
using Formax.Application.DTOs.Teams;
using Formax.Application.Interfaces;
using Formax.Application.Services.Intelligence;
using Formax.Engine.Core.ExternalTrends;
using Formax.Engine.Core.Trends;

namespace Formax.Application.Services.Recommendation;

public class RecommendationEngine : IRecommendationEngine
{
    private readonly GlobalTrendService _globalTrendService;
    private readonly IExternalTrendProvider _externalProvider;
    private readonly ExternalTrendService _externalTrendService;
    private readonly UserTrendService _userTrendService;
    private readonly UserProfileEngine _userProfileEngine;

    public RecommendationEngine(
        GlobalTrendService globalTrendService,
        IExternalTrendProvider externalProvider,
        ExternalTrendService externalTrendService,
        UserTrendService userTrendService,
        UserProfileEngine userProfileEngine)
    {
        _globalTrendService = globalTrendService;
        _externalProvider = externalProvider;
        _externalTrendService = externalTrendService;
        _userTrendService = userTrendService;
        _userProfileEngine = userProfileEngine;
    }

    public async Task<List<RecommendationCardDto>> BuildRecommendationFeed(
        int userId,
        List<HomeRadarMatchDto> radarMatches)
    {
        var list = new List<RecommendationCardDto>();

        var profile = await _userProfileEngine.Build(userId);

        foreach (var match in radarMatches)
        {
            var teamA = match.Teams?.Home ?? "HOME";
            var teamB = match.Teams?.Away ?? "AWAY";

            // 🔥 GLOBAL
            var globalScore = await _globalTrendService.GetGlobalScore(match);

            // 🔥 MARKET
            var externalMomentum = await _externalProvider.GetMarketMomentum(match.MatchId);
            var odds = externalMomentum == 0 ? 0.5 : externalMomentum;

            // 🔥 EXTERNAL
            var ext = _externalTrendService.Get(match.MatchId);
            var marketConfidence = ext.MarketConfidence;
            var oddsMovement = ext.OddsMovement;
            var isHot = ext.IsHot;

            // 🔥 USER
            var behavior = await _userTrendService.Calculate(
                userId,
                match.MatchId,
                teamA,
                teamB,
                odds
            );

            if (behavior <= 0.21)
                behavior = 0.25;

            var freshness = match.TimeProximityScore / 100.0;

            double profileBoost = 0;

            if (odds > 0.6)
                profileBoost += profile.RiskLevel * 0.2;

            if (odds < 0.4)
                profileBoost += (1 - profile.RiskLevel) * 0.2;

            profileBoost += profile.EngagementScore * 0.1;

            // 🔥 FINAL SCORE
            var finalScore =
                (behavior * 0.25) +
                (odds * 0.15) +
                (globalScore * 0.30) +
                (marketConfidence * 0.15) +
                (oddsMovement * 0.10) +
                (Math.Pow(marketConfidence, 3) * 0.25) + // 🔥 spike core
                (freshness * 0.03) +
                (profileBoost * 0.02);

            // 🔥 EXTRA SPIKE BOOST
            finalScore += (marketConfidence * 0.05);

            // 🔥 HOT BOOST
            if (isHot)
                finalScore += 0.12;

            // 🔥 HARD SPIKE TRIGGER
            if (marketConfidence > 0.85)
                finalScore += 0.10;

            // 🔥 CLAMP
            finalScore = Math.Clamp(finalScore, 0, 1);

            // 🔥 SOFT CAP (daha doğal dağılım)
            finalScore = Math.Pow(finalScore, 1.1);

            // 🔥 HARD LIMIT (overboost engelle)
            if (finalScore > 0.92)
                finalScore = 0.92;

            // 🔥 SPIKE LIMITER (çok uçları yumuşat)
            if (marketConfidence > 0.9)
                finalScore *= 0.95;

            var card = new RecommendationCardDto
            {
                MatchId = match.MatchId,

                HomeTeam = new TeamDto { Name = teamA },
                AwayTeam = new TeamDto { Name = teamB },

                TeamA = teamA,
                TeamB = teamB,

                Score = finalScore * 100,

                MarketTrendScore = odds,
                UserTrendScore = behavior,
                GlobalTrendScore = globalScore,
                ExternalMomentum = odds,

                CrossUserScore = globalScore,

                MomentumScore = oddsMovement,
                SpikeScore = marketConfidence,
                DirectionScore = isHot ? 1 : 0
            };

            list.Add(card);
        }

        var pipeline = new FeedRankingPipeline();
        return await pipeline.Rank(userId, list);
    }

    private string GetLabel(double score)
    {
        if (score > 0.8) return "HOT";
        if (score > 0.6) return "GOOD";
        if (score > 0.4) return "RISKY";
        return "LOW";
    }
}