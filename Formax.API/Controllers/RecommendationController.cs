using Formax.Application.DTOs.Home;
using Formax.Application.DTOs.Recommendations;
using Formax.Application.Interfaces;
using Formax.Application.Services.Feed;
using Formax.Application.UseCases.Home;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecommendationController : ControllerBase
{
    private readonly IRecommendationEngine _engine;
    private readonly AdaptiveRankingService _ranking;
    private readonly GetHomeRadarUseCase _radar;

    public RecommendationController(
        IRecommendationEngine engine,
        AdaptiveRankingService ranking,
        GetHomeRadarUseCase radar)
    {
        _engine = engine;
        _ranking = ranking;
        _radar = radar;
    }

    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(int? userId)
    {
        // 🔥 TEST MODE → DEFAULT USER
        var uid = userId ?? 1;

        Console.WriteLine($"[FEED USER ID]: {uid}");

        var radarResponse = await _radar.ExecuteAsync(uid, false, DateTime.UtcNow);
        var radar = radarResponse.Matches?.ToList() ?? new List<HomeRadarMatchDto>();

        if (radar.Count == 0)
            return Ok(new List<RecommendationCardDto>());

        var list = await _engine.BuildRecommendationFeed(uid, radar);

        if (list == null || list.Count == 0)
            return Ok(new List<RecommendationCardDto>());

        list = _ranking.Rank(list, "default");

        foreach (var x in list)
        {
            x.InsightLabel = Decide(x.Score, x.TrendImpact);
        }

        return Ok(list);
    }

    private string Decide(double score, double anomaly)
    {
        if (score >= 88 && anomaly < 0.20)
            return "VALUE";

        if (score >= 75)
            return anomaly > 0.75 ? "RISKY" : "HOT";

        if (score >= 60)
            return anomaly > 0.80 ? "RISKY" : "GOOD";

        if (score >= 45)
            return anomaly > 0.70 ? "RISKY" : "BAD";

        return "BAD";
    }
}